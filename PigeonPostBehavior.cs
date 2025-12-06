using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.MountAndBlade;
using BannerPigeon.Models;
using BannerPigeon.Settings;

namespace BannerPigeon
{
	public class PigeonPostBehavior : CampaignBehaviorBase
	{
		private List<PigeonLetter> _activeLetters;
		private Hero _currentLetterRecipient;
		private Queue<PigeonLetter> _conversationQueue;
		private PigeonLetter _currentProcessingLetter;

		// Save-friendly data structures
		private List<Hero> _savedTargetLords;
		private List<Settlement> _savedOriginSettlements;
		private List<float> _savedSentTimes;
		private List<float> _savedResponseTimes;
		private List<bool> _savedIsDelivered;

		public PigeonPostBehavior()
		{
			_activeLetters = new List<PigeonLetter>();
			_conversationQueue = new Queue<PigeonLetter>();
		}

		public override void RegisterEvents()
		{
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
			CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
		}

		public override void SyncData(IDataStore dataStore)
		{
			// Save data
			if (dataStore.IsSaving)
			{
				_savedTargetLords = new List<Hero>();
				_savedOriginSettlements = new List<Settlement>();
				_savedSentTimes = new List<float>();
				_savedResponseTimes = new List<float>();
				_savedIsDelivered = new List<bool>();

				foreach (var letter in _activeLetters)
				{
					_savedTargetLords.Add(letter.TargetLord);
					_savedOriginSettlements.Add(letter.OriginSettlement);
					_savedSentTimes.Add(letter.SentTime.ElapsedDaysUntilNow);
					_savedResponseTimes.Add(letter.ResponseTime.ElapsedDaysUntilNow);
					_savedIsDelivered.Add(letter.IsDelivered);
				}
			}

			dataStore.SyncData("_pigeonTargetLords", ref _savedTargetLords);
			dataStore.SyncData("_pigeonOriginSettlements", ref _savedOriginSettlements);
			dataStore.SyncData("_pigeonSentTimes", ref _savedSentTimes);
			dataStore.SyncData("_pigeonResponseTimes", ref _savedResponseTimes);
			dataStore.SyncData("_pigeonIsDelivered", ref _savedIsDelivered);
			dataStore.SyncData("_pigeonCurrentRecipient", ref _currentLetterRecipient);

			// Load data
			if (dataStore.IsLoading)
			{
				_activeLetters = new List<PigeonLetter>();
				
				if (_savedTargetLords != null && _savedTargetLords.Count > 0)
				{
					for (int i = 0; i < _savedTargetLords.Count; i++)
					{
						var letter = new PigeonLetter
						{
							TargetLord = _savedTargetLords[i],
							OriginSettlement = _savedOriginSettlements[i],
							SentTime = CampaignTime.Now - CampaignTime.Days(_savedSentTimes[i]),
							ResponseTime = CampaignTime.Now - CampaignTime.Days(_savedResponseTimes[i]),
							IsDelivered = _savedIsDelivered[i]
						};
						_activeLetters.Add(letter);
					}
				}
			}
		}

		private void OnSessionLaunched(CampaignGameStarter starter)
		{
			AddGameMenus(starter);
		}

		private void AddGameMenus(CampaignGameStarter starter)
		{
			// Add pigeon post option to town menu
			starter.AddGameMenuOption("town", "town_pigeon_post", "Send a carrier pigeon",
				CanUsePigeonInTown,
				args => GameMenu.SwitchToMenu("pigeon_select_recipient"),
				false, 5);

			// Add pigeon post option to castle menu
			starter.AddGameMenuOption("castle", "castle_pigeon_post", "Send a carrier pigeon",
				CanUsePigeonInCastle,
				args => GameMenu.SwitchToMenu("pigeon_select_recipient"),
				false, 5);

			// Recipient selection menu
			starter.AddGameMenu("pigeon_select_recipient", "Select a lord to contact via carrier pigeon:",
				null);

			starter.AddGameMenuOption("pigeon_select_recipient", "pigeon_contact_settlement_owner", 
				"Contact the settlement owner",
				CanContactSettlementOwner,
				OnContactSettlementOwner,
				false, 0);

			starter.AddGameMenuOption("pigeon_select_recipient", "pigeon_contact_kingdom_leader",
				"Contact the kingdom leader",
				CanContactKingdomLeader,
				OnContactKingdomLeader,
				false, 1);

			starter.AddGameMenuOption("pigeon_select_recipient", "pigeon_contact_caravan",
				"Contact a caravan leader",
				CanContactCaravan,
				OnContactCaravan,
				false, 2);

			starter.AddGameMenuOption("pigeon_select_recipient", "pigeon_back",
				"Back",
				args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
				args => GameMenu.SwitchToMenu(Settlement.CurrentSettlement.IsTown ? "town" : "castle"),
				true);

			// Confirmation menu
			starter.AddGameMenu("pigeon_confirm", "{PIGEON_CONFIRMATION_TEXT}",
				OnPigeonConfirmInit);

			starter.AddGameMenuOption("pigeon_confirm", "pigeon_send",
				"Send the pigeon",
				CanAffordPigeon,
				OnSendPigeon,
				false, 0);

			starter.AddGameMenuOption("pigeon_confirm", "pigeon_cancel",
				"Cancel",
				args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
				args => GameMenu.SwitchToMenu("pigeon_select_recipient"),
				true, 1);
		}

		private bool CanUsePigeonInTown(MenuCallbackArgs args)
		{
			try
			{
				if (PigeonSettings.Instance == null) return false;
				args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
				return PigeonSettings.Instance.EnableInTowns && Settlement.CurrentSettlement?.IsTown == true;
			}
			catch
			{
				return false;
			}
		}

		private bool CanUsePigeonInCastle(MenuCallbackArgs args)
		{
			try
			{
				if (PigeonSettings.Instance == null) return false;
				args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
				return PigeonSettings.Instance.EnableInCastles && Settlement.CurrentSettlement?.IsCastle == true;
			}
			catch
			{
				return false;
			}
		}

		private bool CanContactSettlementOwner(MenuCallbackArgs args)
		{
			try
			{
				var owner = Settlement.CurrentSettlement?.OwnerClan?.Leader;
				if (owner == null || owner == Hero.MainHero)
				{
					args.IsEnabled = false;
					args.Tooltip = new TextObject("This settlement has no owner or you own it.");
					return false;
				}

				if (owner.IsPrisoner)
				{
					args.IsEnabled = false;
					args.Tooltip = new TextObject("The settlement owner is currently a prisoner.");
					return false;
				}

				args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private bool CanContactKingdomLeader(MenuCallbackArgs args)
		{
			try
			{
				var kingdom = Settlement.CurrentSettlement?.OwnerClan?.Kingdom;
				var leader = kingdom?.Leader;
				
				if (leader == null || leader == Hero.MainHero)
				{
					args.IsEnabled = false;
					args.Tooltip = new TextObject("This settlement's kingdom has no leader or you are the leader.");
					return false;
				}

				if (leader.IsPrisoner)
				{
					args.IsEnabled = false;
					args.Tooltip = new TextObject("The kingdom leader is currently a prisoner.");
					return false;
				}

				args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void OnContactSettlementOwner(MenuCallbackArgs args)
		{
			_currentLetterRecipient = Settlement.CurrentSettlement?.OwnerClan?.Leader;
			GameMenu.SwitchToMenu("pigeon_confirm");
		}

		private void OnContactKingdomLeader(MenuCallbackArgs args)
		{
			_currentLetterRecipient = Settlement.CurrentSettlement?.OwnerClan?.Kingdom?.Leader;
			GameMenu.SwitchToMenu("pigeon_confirm");
		}

		private bool CanContactCaravan(MenuCallbackArgs args)
		{
			try
			{
				var caravans = MobileParty.All?.Where(p => p != null && p.IsCaravan && p.Owner == Hero.MainHero && p.LeaderHero != null).ToList();
				if (caravans == null || !caravans.Any())
				{
					args.IsEnabled = false;
					args.Tooltip = new TextObject("You have no active caravans with leaders.");
					return false;
				}
				
				args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void OnContactCaravan(MenuCallbackArgs args)
		{
			var caravans = MobileParty.All.Where(p => p.IsCaravan && p.Owner == Hero.MainHero && p.LeaderHero != null).ToList();
			
		var inquiryElements = new List<InquiryElement>();
		foreach (var caravan in caravans)
		{
			// ImageIdentifier not available in this Bannerlord version, using null for now
			inquiryElements.Add(new InquiryElement(caravan.LeaderHero, caravan.Name.ToString(), null));
		}			// Fixed MultiSelectionInquiryData signature (added minSelectable)
			MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
				"Select Caravan Leader",
				"Choose which caravan leader you want to send a pigeon to:",
				inquiryElements,
				true,
				1, // minSelectable
				1, // maxSelectable
				"Select",
				"Cancel",
				(selectedElements) => 
				{
					if (selectedElements.Any())
					{
						var selectedHero = selectedElements.First().Identifier as Hero;
						if (selectedHero != null)
						{
							_currentLetterRecipient = selectedHero;
							GameMenu.SwitchToMenu("pigeon_confirm");
						}
					}
				},
				(exited) => { },
				""
			));
		}

	private void OnPigeonConfirmInit(MenuCallbackArgs args)
	{
		if (_currentLetterRecipient != null)
		{
			int cost = PigeonSettings.Instance.PigeonCost;
			int days = CalculateResponseDays(_currentLetterRecipient);
			
			TextObject text = new TextObject(
				"Send a carrier pigeon to {LORD_NAME} for {COST}{GOLD_ICON}? " +
				"You should receive a response in approximately {DAYS} days.");
			
			text.SetTextVariable("LORD_NAME", _currentLetterRecipient.Name);
			text.SetTextVariable("COST", cost);
			text.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");
			text.SetTextVariable("DAYS", days);
			
			MBTextManager.SetTextVariable("PIGEON_CONFIRMATION_TEXT", text);
		}
	}

	private int CalculateResponseDays(Hero recipient)
	{
		int days = PigeonSettings.Instance.ResponseDays;

		if (PigeonSettings.Instance.UseRealisticTravelTime && Settlement.CurrentSettlement != null)
		{
			IMapPoint targetPoint = null;
			if (recipient.PartyBelongedTo != null)
			{
				targetPoint = recipient.PartyBelongedTo;
			}
			else if (recipient.CurrentSettlement != null)
			{
				targetPoint = recipient.CurrentSettlement;
			}

			if (targetPoint != null)
			{
				float x1 = Settlement.CurrentSettlement.GatePosition.X;
				float y1 = Settlement.CurrentSettlement.GatePosition.Y;
				float x2 = 0, y2 = 0;

				if (targetPoint is MobileParty mp)
				{
					// Use VisualPosition for 2D coordinates
					x2 = mp.VisualPosition2DWithoutError.x;
					y2 = mp.VisualPosition2DWithoutError.y;
				}
				else if (targetPoint is Settlement s)
				{
					x2 = s.GatePosition.X;
					y2 = s.GatePosition.Y;
				}

				float dx = x2 - x1;
				float dy = y2 - y1;
				float distance = (float)Math.Sqrt(dx*dx + dy*dy);
				
				float speed = (float)PigeonSettings.Instance.PigeonSpeed;
				float travelDays = distance / speed;
				days = 1 + (int)Math.Ceiling(travelDays);
			}
		}

		return days;
	}		private bool CanAffordPigeon(MenuCallbackArgs args)
		{
			int cost = PigeonSettings.Instance.PigeonCost;
			
			if (Hero.MainHero.Gold < cost)
			{
				args.IsEnabled = false;
				args.Tooltip = new TextObject($"You need {cost} gold to send a carrier pigeon.");
				return false;
			}

			args.optionLeaveType = GameMenuOption.LeaveType.Continue;
			return true;
		}

		private void OnSendPigeon(MenuCallbackArgs args)
		{
			int cost = PigeonSettings.Instance.PigeonCost;
			int days = CalculateResponseDays(_currentLetterRecipient);

			// Deduct gold - give to settlement owner if possible
			var settlementOwner = Settlement.CurrentSettlement?.OwnerClan?.Leader;
			if (settlementOwner != null && settlementOwner != Hero.MainHero)
			{
				GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, settlementOwner, cost, false);
			}
			else
			{
				GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, false);
			}

			// Create and store the letter
			var letter = new PigeonLetter(_currentLetterRecipient, Settlement.CurrentSettlement, days);
			_activeLetters.Add(letter);

			InformationManager.DisplayMessage(new InformationMessage(
				$"Carrier pigeon sent to {_currentLetterRecipient.Name}. Expect a response in {days} days.",
				Colors.Green));

			_currentLetterRecipient = null;
			GameMenu.SwitchToMenu(Settlement.CurrentSettlement.IsTown ? "town" : "castle");
		}

		private void OnDailyTick()
		{
			try
			{
				// Check for letters ready for response
				var readyLetters = _activeLetters.Where(l => l.IsReadyForResponse() && !_conversationQueue.Contains(l)).ToList();

				foreach (var letter in readyLetters)
				{
					if (letter.TargetLord != null && letter.TargetLord.IsAlive && !letter.TargetLord.IsPrisoner)
					{
						_conversationQueue.Enqueue(letter);
					}
					else
					{
						// Lord is dead or prisoner, mark delivered but don't start conversation
						letter.IsDelivered = true;
						InformationManager.DisplayMessage(new InformationMessage(
							$"A carrier pigeon returned from {(letter.TargetLord?.Name?.ToString() ?? "unknown")} but they could not be reached.",
							Colors.Red));
					}
				}

				ProcessConversationQueue();

				// Clean up delivered letters
				_activeLetters.RemoveAll(l => l.IsDelivered);
			}
			catch
			{
				// Silently handle errors to prevent crash during daily tick
			}
		}

		private void ProcessConversationQueue()
		{
			try
			{
				if (_conversationQueue.Count == 0)
					return;

				// Don't start conversation during scene transitions or if already in conversation
				if (Campaign.Current?.ConversationManager == null || 
				    Campaign.Current.ConversationManager.IsConversationInProgress ||
				    CampaignMission.Current != null ||
				    GameStateManager.Current?.ActiveState == null)
					return;

				var letter = _conversationQueue.Peek();
				if (letter?.TargetLord == null)
					return;

				_currentProcessingLetter = letter;

				if (PigeonSettings.Instance?.ShowNotifications == true)
				{
					InformationManager.DisplayMessage(new InformationMessage(
						$"You received a response from {letter.TargetLord.Name}!",
						Colors.Cyan));
				}

				StartConversationWithLord(letter.TargetLord);
			}
			catch
			{
				// Silently handle errors to prevent crash
			}
		}

		private void OnConversationEnded(IEnumerable<CharacterObject> characters)
		{
			if (_currentProcessingLetter != null)
			{
				_currentProcessingLetter.IsDelivered = true;
				if (_conversationQueue.Count > 0 && _conversationQueue.Peek() == _currentProcessingLetter)
				{
					_conversationQueue.Dequeue();
				}
				_currentProcessingLetter = null;
				
				// Process next in queue
				ProcessConversationQueue();
			}
		}

		private void StartConversationWithLord(Hero lord)
		{
			// This triggers the standard lord conversation dialog
			if (CampaignMission.Current == null)
			{
				PartyBase targetParty = null;
				if (lord.PartyBelongedTo != null)
				{
					targetParty = lord.PartyBelongedTo.Party;
				}
				else if (lord.CurrentSettlement != null)
				{
					targetParty = lord.CurrentSettlement.Party;
				}

				if (targetParty != null)
				{
					// For caravans, don't set context to avoid triggering caravan encounter behaviors
					if (lord.PartyBelongedTo == null || !lord.PartyBelongedTo.IsCaravan)
					{
						Campaign.Current.CurrentConversationContext = ConversationContext.Default;
					}
					
					// If player is on campaign map, start a conversation encounter
					CampaignMapConversation.OpenConversation(
						new ConversationCharacterData(CharacterObject.PlayerCharacter, Hero.MainHero.PartyBelongedTo?.Party),
						new ConversationCharacterData(lord.CharacterObject, targetParty));
				}
			}
		}
	}
}

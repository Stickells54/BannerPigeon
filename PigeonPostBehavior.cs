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
			args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
			return PigeonSettings.Instance.EnableInTowns && Settlement.CurrentSettlement?.IsTown == true;
		}

		private bool CanUsePigeonInCastle(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
			return PigeonSettings.Instance.EnableInCastles && Settlement.CurrentSettlement?.IsCastle == true;
		}

		private bool CanContactSettlementOwner(MenuCallbackArgs args)
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

		private bool CanContactKingdomLeader(MenuCallbackArgs args)
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

		private void OnPigeonConfirmInit(MenuCallbackArgs args)
		{
			if (_currentLetterRecipient != null)
			{
				int cost = PigeonSettings.Instance.PigeonCost;
				int days = PigeonSettings.Instance.ResponseDays;
				
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

		private bool CanAffordPigeon(MenuCallbackArgs args)
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
			int days = PigeonSettings.Instance.ResponseDays;

			if (PigeonSettings.Instance.UseRealisticTravelTime)
			{
				IMapPoint targetPoint = null;
				if (_currentLetterRecipient.PartyBelongedTo != null)
				{
					targetPoint = _currentLetterRecipient.PartyBelongedTo;
				}
				else if (_currentLetterRecipient.CurrentSettlement != null)
				{
					targetPoint = _currentLetterRecipient.CurrentSettlement;
				}

				if (targetPoint != null)
				{
					float x1 = Settlement.CurrentSettlement.GatePosition.X;
					float y1 = Settlement.CurrentSettlement.GatePosition.Y;
					float x2 = 0, y2 = 0;

					if (targetPoint is MobileParty mp)
					{
						// Use dynamic to access position properties to avoid compile errors if types are tricky
						dynamic dmp = mp;
						try {
							x2 = dmp.Position2D.X;
							y2 = dmp.Position2D.Y;
						} catch {
							try {
								x2 = dmp.Position.X;
								y2 = dmp.Position.Y;
							} catch {
								// Fallback
								x2 = x1 + 250; // Approx 5 days
								y2 = y1;
							}
						}
					}
					else if (targetPoint is Settlement s)
					{
						x2 = s.GatePosition.X;
						y2 = s.GatePosition.Y;
					}

					float dx = x2 - x1;
					float dy = y2 - y1;
					float distance = (float)Math.Sqrt(dx*dx + dy*dy);
					
					// MapDistanceModel returns distance in map units.
					// Let's assume a base processing time of 1 day + travel time.
					// A typical distance across map is ~500-800?
					// Let's use a simple divisor. 50 map units per day for pigeon seems reasonable.
					float speed = (float)PigeonSettings.Instance.PigeonSpeed;
					float travelDays = distance / speed;
					days = 1 + (int)Math.Ceiling(travelDays);
				}
			}

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
						$"A carrier pigeon returned from {letter.TargetLord.Name} but they could not be reached.",
						Colors.Red));
				}
			}

			ProcessConversationQueue();

			// Clean up delivered letters
			_activeLetters.RemoveAll(l => l.IsDelivered);
		}

		private void ProcessConversationQueue()
		{
			if (_conversationQueue.Count == 0 || Campaign.Current.ConversationManager.IsConversationInProgress)
				return;

			var letter = _conversationQueue.Peek();
			_currentProcessingLetter = letter;

			if (PigeonSettings.Instance.ShowNotifications)
			{
				InformationManager.DisplayMessage(new InformationMessage(
					$"You received a response from {letter.TargetLord.Name}!",
					Colors.Cyan));
			}

			StartConversationWithLord(letter.TargetLord);
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
					// If player is on campaign map, start a conversation encounter
					Campaign.Current.CurrentConversationContext = ConversationContext.Default;
					CampaignMapConversation.OpenConversation(
						new ConversationCharacterData(CharacterObject.PlayerCharacter, Hero.MainHero.PartyBelongedTo?.Party),
						new ConversationCharacterData(lord.CharacterObject, targetParty));
				}
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using BannerPigeon.Models;
using BannerPigeon.Settings;

namespace BannerPigeon
{
	public class PigeonPostBehavior : CampaignBehaviorBase
	{
		private List<PigeonLetter> _activeLetters;
		private Hero _currentLetterRecipient;

		// Save-friendly data structures
		private List<Hero> _savedTargetLords;
		private List<Settlement> _savedOriginSettlements;
		private List<float> _savedSentTimes;
		private List<float> _savedResponseTimes;
		private List<bool> _savedIsDelivered;

		public PigeonPostBehavior()
		{
			_activeLetters = new List<PigeonLetter>();
		}

		public override void RegisterEvents()
		{
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
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

			// Deduct gold
			GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, false);

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
			var readyLetters = _activeLetters.Where(l => l.IsReadyForResponse()).ToList();

			foreach (var letter in readyLetters)
			{
				if (letter.TargetLord != null && letter.TargetLord.IsAlive)
				{
					if (PigeonSettings.Instance.ShowNotifications)
					{
						InformationManager.DisplayMessage(new InformationMessage(
							$"You received a response from {letter.TargetLord.Name}!",
							Colors.Cyan));
					}

					// Initiate conversation with the lord
					StartConversationWithLord(letter.TargetLord);
				}

				letter.IsDelivered = true;
			}

			// Clean up delivered letters
			_activeLetters.RemoveAll(l => l.IsDelivered);
		}

		private void StartConversationWithLord(Hero lord)
		{
			// This triggers the standard lord conversation dialog
			if (CampaignMission.Current == null && lord.PartyBelongedTo != null)
			{
				// If player is on campaign map, start a conversation encounter
				Campaign.Current.CurrentConversationContext = ConversationContext.Default;
				CampaignMapConversation.OpenConversation(
					new ConversationCharacterData(CharacterObject.PlayerCharacter, Hero.MainHero.PartyBelongedTo?.Party),
					new ConversationCharacterData(lord.CharacterObject, lord.PartyBelongedTo.Party));
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

			// Consolidated recipient selection menu - 3 clean options
			starter.AddGameMenu("pigeon_select_recipient", "Select who to send a carrier pigeon to:",
				null);

			starter.AddGameMenuOption("pigeon_select_recipient", "pigeon_send_to_lord",
				"Send letter to a lord",
				CanContactAnyLord,
				OnContactAnyLord,
				false, 0);

			starter.AddGameMenuOption("pigeon_select_recipient", "pigeon_send_to_caravan",
				"Send letter to a caravan",
				CanContactCaravan,
				OnContactCaravan,
				false, 1);

			starter.AddGameMenuOption("pigeon_select_recipient", "pigeon_send_to_port",
				"Send letter to a port city (Fleet Recall)",
				CanSendToPort,
				OnSendToPort,
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
				inquiryElements.Add(new InquiryElement(caravan.LeaderHero, caravan.Name.ToString(), null));
			}

			MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
				"Select Caravan",
				"Choose which caravan you want to send a pigeon to:",
				inquiryElements,
				true,
				1,
				1,
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
				"",
				true
			));
		}

		private bool CanContactAnyLord(MenuCallbackArgs args)
		{
			try
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void OnContactAnyLord(MenuCallbackArgs args)
		{
			var lords = Hero.AllAliveHeroes
				.Where(h => h != null && h != Hero.MainHero && !h.IsPrisoner && h.IsLord && h.IsAlive)
				.OrderBy(h => h.Name.ToString())
				.ToList();

			var inquiryElements = new List<InquiryElement>();
			foreach (var lord in lords)
			{
				string clanInfo = lord.Clan != null ? $" ({lord.Clan.Name})" : "";
				inquiryElements.Add(new InquiryElement(lord, lord.Name.ToString() + clanInfo, null));
			}

			MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
				"Select Lord",
				"Choose which lord you want to send a pigeon to:",
				inquiryElements,
				true,
				1,
				1,
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
				"",
				true
			));
		}

		private bool CanSendToPort(MenuCallbackArgs args)
		{
			try
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
				var ports = GetPortCities();
				if (ports == null || !ports.Any())
				{
					args.IsEnabled = false;
					args.Tooltip = new TextObject("No port cities found.");
					return false;
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void OnSendToPort(MenuCallbackArgs args)
		{
			var ports = GetPortCities();
			
			var inquiryElements = new List<InquiryElement>();
			foreach (var port in ports)
			{
				string ownerInfo = port.OwnerClan != null ? $" ({port.OwnerClan.Name})" : "";
				inquiryElements.Add(new InquiryElement(port, port.Name.ToString() + ownerInfo, null));
			}

			MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
				"Select Port City",
				"Choose which port to recall your fleet to:",
				inquiryElements,
				true,
				1,
				1,
				"Send",
				"Cancel",
				(selectedElements) =>
				{
					if (selectedElements.Any())
					{
						var selectedPort = selectedElements.First().Identifier as Settlement;
						if (selectedPort != null)
						{
							SendFleetRecallLetter(selectedPort);
						}
					}
				},
				(exited) => { },
				"",
				true
			));
		}

		private List<Settlement> GetPortCities()
		{
			var ports = new List<Settlement>();
			foreach (var settlement in Settlement.All)
			{
				if (settlement != null && settlement.IsTown && IsPortCity(settlement))
				{
					ports.Add(settlement);
				}
			}
			return ports.OrderBy(s => s.Name.ToString()).ToList();
		}

		private bool IsPortCity(Settlement settlement)
		{
			try
			{
				var type = settlement.GetType();
				
				var portProp = type.GetProperty("Port", BindingFlags.Public | BindingFlags.Instance);
				if (portProp != null)
				{
					var portValue = portProp.GetValue(settlement);
					if (portValue != null) return true;
				}

				var hasPortProp = type.GetProperty("HasPort", BindingFlags.Public | BindingFlags.Instance);
				if (hasPortProp != null)
				{
					var hasPort = hasPortProp.GetValue(settlement);
					if (hasPort is bool b && b) return true;
				}

				string name = settlement.Name?.ToString()?.ToLower() ?? "";
				return name.Contains("port") || name.Contains("harbor") || name.Contains("harbour");
			}
			catch
			{
				return false;
			}
		}

		private int CalculateDeliveryDaysToSettlement(Settlement target)
		{
			int days = PigeonSettings.Instance.ResponseDays;

			if (PigeonSettings.Instance.UseRealisticTravelTime && Settlement.CurrentSettlement != null)
			{
				float x1 = Settlement.CurrentSettlement.GatePosition.X;
				float y1 = Settlement.CurrentSettlement.GatePosition.Y;
				float x2 = target.GatePosition.X;
				float y2 = target.GatePosition.Y;

				float dx = x2 - x1;
				float dy = y2 - y1;
				float distance = (float)Math.Sqrt(dx * dx + dy * dy);

				float speed = (float)PigeonSettings.Instance.PigeonSpeed;
				float travelDays = distance / speed;
				days = 1 + (int)Math.Ceiling(travelDays);
			}

			return days;
		}

		private void SendFleetRecallLetter(Settlement targetPort)
		{
			int cost = PigeonSettings.Instance.PigeonCost;
			int days = CalculateDeliveryDaysToSettlement(targetPort);

			var settlementOwner = Settlement.CurrentSettlement?.OwnerClan?.Leader;
			if (settlementOwner != null && settlementOwner != Hero.MainHero)
			{
				GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, settlementOwner, cost, false);
			}
			else
			{
				GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, false);
			}

			var letter = new PigeonLetter(targetPort, Settlement.CurrentSettlement, days);
			_activeLetters.Add(letter);

			InformationManager.DisplayMessage(new InformationMessage(
				$"Fleet recall letter sent to {targetPort.Name}. Your fleet will be called in {days} days.",
				Colors.Green));

			GameMenu.SwitchToMenu(Settlement.CurrentSettlement.IsTown ? "town" : "castle");
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
			int days = CalculateResponseDays(_currentLetterRecipient);

			var settlementOwner = Settlement.CurrentSettlement?.OwnerClan?.Leader;
			if (settlementOwner != null && settlementOwner != Hero.MainHero)
			{
				GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, settlementOwner, cost, false);
			}
			else
			{
				GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, false);
			}

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
				var readyLetters = _activeLetters.Where(l => l.IsReadyForResponse() && !_conversationQueue.Contains(l)).ToList();

				foreach (var letter in readyLetters)
				{
					if (letter.IsFleetRecall)
					{
						ProcessFleetRecallLetter(letter);
						letter.IsDelivered = true;
						continue;
					}

					if (letter.TargetLord != null && letter.TargetLord.IsAlive && !letter.TargetLord.IsPrisoner)
					{
						_conversationQueue.Enqueue(letter);
					}
					else
					{
						letter.IsDelivered = true;
						InformationManager.DisplayMessage(new InformationMessage(
							$"A carrier pigeon returned from {(letter.TargetLord?.Name?.ToString() ?? "unknown")} but they could not be reached.",
							Colors.Red));
					}
				}

				ProcessConversationQueue();
				_activeLetters.RemoveAll(l => l.IsDelivered);
			}
			catch
			{
			}
		}

		private void ProcessFleetRecallLetter(PigeonLetter letter)
		{
			if (letter.TargetSettlement == null)
			{
				InformationManager.DisplayMessage(new InformationMessage(
					"Fleet recall failed: No target port specified.",
					Colors.Red));
				return;
			}

			bool success = TryCallFleetToPort(letter.TargetSettlement);
			
			if (success)
			{
				InformationManager.DisplayMessage(new InformationMessage(
					$"Your fleet has been called to {letter.TargetSettlement.Name}!",
					Colors.Green));
			}
			else
			{
				InformationManager.DisplayMessage(new InformationMessage(
					$"Fleet recall to {letter.TargetSettlement.Name} could not be completed. (API not found or no fleet available)",
					Colors.Yellow));
			}
		}

		private bool TryCallFleetToPort(Settlement targetPort)
		{
			try
			{
				var mainParty = MobileParty.MainParty;
				if (mainParty == null)
				{
					// InformationManager.DisplayMessage(new InformationMessage("[Debug] MainParty is null", Colors.Red));
					return false;
				}

				// Strategy 1: Try Anchor property on MobileParty
				var partyType = mainParty.GetType();
				var anchorProp = partyType.GetProperty("Anchor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

				if (anchorProp != null)
				{
					// InformationManager.DisplayMessage(new InformationMessage($"[Debug] Found Anchor property on MobileParty", Colors.Cyan));
					var anchor = anchorProp.GetValue(mainParty);
					if (anchor != null)
					{
						// InformationManager.DisplayMessage(new InformationMessage($"[Debug] Anchor object type: {anchor.GetType().FullName}", Colors.Cyan));
						
						var anchorType = anchor.GetType();
						var callFleetMethod = anchorType.GetMethod("CallFleet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						
						if (callFleetMethod != null)
						{
							// InformationManager.DisplayMessage(new InformationMessage($"[Debug] Found CallFleet method!", Colors.Green));
							callFleetMethod.Invoke(anchor, new object[] { targetPort });
							return true;
						}
						else
						{
							// var methods = anchorType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
							// var methodNames = string.Join(", ", methods.Take(10).Select(m => m.Name));
							// InformationManager.DisplayMessage(new InformationMessage($"[Debug] Anchor methods: {methodNames}", Colors.Yellow));
						}
					}
					else
					{
						// InformationManager.DisplayMessage(new InformationMessage("[Debug] Anchor property is null", Colors.Yellow));
					}
				}
				else
				{
					// var props = partyType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					// var relatedProps = props.Where(p => 
					// 	p.Name.Contains("Anchor") || p.Name.Contains("Fleet") || 
					// 	p.Name.Contains("Naval") || p.Name.Contains("Ship"))
					// 	.Select(p => p.Name);
					// var propList = string.Join(", ", relatedProps);
					// InformationManager.DisplayMessage(new InformationMessage($"[Debug] No Anchor on MobileParty. Related props: {propList}", Colors.Yellow));
				}

				// Strategy 2: Try Campaign.Current
				var campaign = Campaign.Current;
				if (campaign != null)
				{
					var campaignType = campaign.GetType();
					var campaignAnchorProp = campaignType.GetProperty("Anchor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (campaignAnchorProp != null)
					{
						// InformationManager.DisplayMessage(new InformationMessage("[Debug] Found Anchor on Campaign", Colors.Cyan));
						var anchor = campaignAnchorProp.GetValue(campaign);
						if (anchor != null)
						{
							var callFleetMethod = anchor.GetType().GetMethod("CallFleet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
							if (callFleetMethod != null)
							{
								callFleetMethod.Invoke(anchor, new object[] { targetPort });
								return true;
							}
						}
					}
				}

				// Strategy 3: Try Clan
				var clan = Hero.MainHero?.Clan;
				if (clan != null)
				{
					var clanType = clan.GetType();
					var clanAnchorProp = clanType.GetProperty("Anchor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (clanAnchorProp != null)
					{
						// InformationManager.DisplayMessage(new InformationMessage("[Debug] Found Anchor on Clan", Colors.Cyan));
						var anchor = clanAnchorProp.GetValue(clan);
						if (anchor != null)
						{
							var callFleetMethod = anchor.GetType().GetMethod("CallFleet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
							if (callFleetMethod != null)
							{
								callFleetMethod.Invoke(anchor, new object[] { targetPort });
								return true;
							}
						}
					}
				}

				// InformationManager.DisplayMessage(new InformationMessage("[Debug] Could not find fleet recall API", Colors.Red));
				return false;
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage($"Error invoking fleet recall: {ex.Message}", Colors.Red));
				return false;
			}
		}

		private void ProcessConversationQueue()
		{
			try
			{
				if (_conversationQueue.Count == 0)
					return;

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
				ProcessConversationQueue();
			}
		}

		private void StartConversationWithLord(Hero lord)
		{
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
					if (lord.PartyBelongedTo == null || !lord.PartyBelongedTo.IsCaravan)
					{
						Campaign.Current.CurrentConversationContext = ConversationContext.Default;
					}
					
					CampaignMapConversation.OpenConversation(
						new ConversationCharacterData(CharacterObject.PlayerCharacter, Hero.MainHero.PartyBelongedTo?.Party),
						new ConversationCharacterData(lord.CharacterObject, targetParty));
				}
			}
		}
	}
}

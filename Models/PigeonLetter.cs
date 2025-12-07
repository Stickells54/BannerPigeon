using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace BannerPigeon.Models
{
	public class PigeonLetter
	{
		public Hero TargetLord;
		public Settlement OriginSettlement;
		public CampaignTime SentTime;
		public CampaignTime ResponseTime;
		public bool IsDelivered;
		
		// Fleet Recall properties
		public bool IsFleetRecall;
		public Settlement TargetSettlement;

		public PigeonLetter()
		{
		}

		public PigeonLetter(Hero targetLord, Settlement origin, int responseDays)
		{
			TargetLord = targetLord;
			OriginSettlement = origin;
			SentTime = CampaignTime.Now;
			ResponseTime = CampaignTime.Now + CampaignTime.Days(responseDays);
			IsDelivered = false;
			IsFleetRecall = false;
			TargetSettlement = null;
		}

		// Constructor for Fleet Recall letters
		public PigeonLetter(Settlement targetPort, Settlement origin, int deliveryDays)
		{
			TargetLord = null;
			OriginSettlement = origin;
			TargetSettlement = targetPort;
			SentTime = CampaignTime.Now;
			ResponseTime = CampaignTime.Now + CampaignTime.Days(deliveryDays);
			IsDelivered = false;
			IsFleetRecall = true;
		}

		public bool IsReadyForResponse()
		{
			return !IsDelivered && CampaignTime.Now >= ResponseTime;
		}
	}
}

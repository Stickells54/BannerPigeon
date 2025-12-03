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
		}

		public bool IsReadyForResponse()
		{
			return !IsDelivered && CampaignTime.Now >= ResponseTime;
		}
	}
}

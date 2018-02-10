using System;

namespace TitanicaParser.Model
{
	public enum Class
	{
		Unknown,
		First, Second, Third,
		DeckCrew, EngineeringCrew, VictuallingCrew
	}
	public enum Sex { Unknown, Male, Female }
	public enum City { Belfast, Southampton, Cherbourg, Queenstown }
	public enum AgeGroup { Unknown, Infant, Child, Teenager, Adult, Senior }
	public enum Deck { A, B, C, D, E, F, G }

	public class TitanicPassenger : IEquatable<TitanicPassenger>
	{
		public Class Class { get; set; }
		public string HonorificPrefix { get; set; }
		public string HonorificSuffix { get; set; }
		public string FamilyName { get; set; }
		public string GivenName { get; set; }
		public Sex Sex { get; set; }
		public bool HasSurvived { get; set; }
		public bool IsGuaranteeGroupMember { get; set; }
		public bool IsServant { get; set; }

		public int? AgeMonths { get; set; }
		public DateTime? BirthDate { get; set; }
		public DateTime? DeathDate { get; set; }

		public string BirthPlace { get; set; }
		public string TicketNo { get; set; }
		public string CabinNo { get; set; }
		public Price TicketPrice { get; set; }
		public City Boarded { get; set; } // Embarked?
		public string JobTitle { get; set; }
		public string Lifeboat { get; set; }
		public string Url { get; set; }

		public string FullName => HonorificPrefix + " " + FamilyName + ", " + GivenName;

		//public Deck Deck { get { } }

		public AgeGroup AgeGroup
		{
			get
			{
				if (!AgeMonths.HasValue) return AgeGroup.Unknown;
				int years = AgeMonths.Value / 12;
				if (years < 2) return AgeGroup.Infant;
				if (years >= 2 && years < 13) return AgeGroup.Child;
				if (years >= 13 && years < 20) return AgeGroup.Teenager;
				if (years >= 20 && years < 60) return AgeGroup.Adult;
				return AgeGroup.Senior;
			}
		}

		public bool Equals(TitanicPassenger pax)
		{
			return (
				Class == pax.Class
				&& string.Equals(HonorificPrefix, pax.HonorificPrefix, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(FamilyName, pax.FamilyName, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(GivenName, pax.GivenName, StringComparison.OrdinalIgnoreCase)
				&& Sex == pax.Sex
				&& HasSurvived == pax.HasSurvived
				&& IsGuaranteeGroupMember == pax.IsGuaranteeGroupMember
				&& IsServant == pax.IsServant
				&& AgeMonths == pax.AgeMonths
				&& string.Equals(TicketNo, pax.TicketNo, StringComparison.OrdinalIgnoreCase)
				&& TicketPrice == pax.TicketPrice
				&& Boarded == pax.Boarded
				&& string.Equals(JobTitle, pax.JobTitle, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(Lifeboat, pax.Lifeboat, StringComparison.OrdinalIgnoreCase)
			);
		}

		public override string ToString() => FullName;
	}

	public static class Titanic
	{
		public static readonly Price Price = new Price("£1500000");
		public static DateTime SunkDate = new DateTime(1912, 4, 15, 2, 20, 0);
	}
}

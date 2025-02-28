﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace BossChecklist
{
	[Flags]
	internal enum NetRecordID : int {
		None = 0,
		PreviousAttemptOnly = 1,
		FirstVictory = 2,
		PersonalBest_Duration = 4,
		PersonalBest_HitsTaken = 8,
		NewPersonalBest = PersonalBest_Duration | PersonalBest_HitsTaken,
		WorldRecord_Duration = 16,
		WorldRecord_HitsTaken = 32,
		WorldRecord = WorldRecord_Duration | WorldRecord_HitsTaken,
		PersonalBest_Reset = 64, // Resetting personal best records will also remove record from World Records
		FirstVictory_Reset = 128,
		ResettingRecord = PersonalBest_Reset | FirstVictory_Reset
	}

	/// <summary>
	/// Record container for player-based records. All personal records should be stored here and saved to a ModPlayer.
	/// </summary>
	public class BossRecord : TagSerializable
	{
		internal string bossKey;
		internal PersonalStats stats = new PersonalStats();

		public static Func<TagCompound, BossRecord> DESERIALIZER = tag => new BossRecord(tag);

		private BossRecord(TagCompound tag) {
			bossKey = tag.Get<string>(nameof(bossKey));
			stats = tag.Get<PersonalStats>(nameof(stats));
		}

		public BossRecord(string bossKey) {
			this.bossKey = bossKey;
		}
		public override string ToString() => $"Personal Records for: '{bossKey}'";

		public TagCompound SerializeData() {
			return new TagCompound {
				{ nameof(bossKey), bossKey },
				{ nameof(stats), stats }
			};
		}
	}

	/// <summary>
	/// Record container for world-based records. All world records should be stored within this class and saved to a ModSystem.
	/// </summary>
	public class WorldRecord : TagSerializable
	{
		internal string bossKey;
		internal WorldStats stats = new WorldStats();

		public static Func<TagCompound, WorldRecord> DESERIALIZER = tag => new WorldRecord(tag);

		private WorldRecord(TagCompound tag) {
			bossKey = tag.Get<string>(nameof(bossKey));
			stats = tag.Get<WorldStats>(nameof(stats));
		}

		public WorldRecord(string bossKey) {
			this.bossKey = bossKey;
		}

		public TagCompound SerializeData() {
			return new TagCompound {
				{ nameof(bossKey), bossKey },
				{ nameof(stats), stats }
			};
		}

		public override string ToString() => $"World Records for: '{bossKey}'";
	}

	/// <summary>
	/// Players are able to set personal records for boss fights.
	/// This will hold the statistics and records of those fights, including the player's previous fight, first victory, and personal best.
	/// <para>[Statistics]</para>
	/// <list type="bullet">
	/// <item> <term>Kills</term> <description>The total amount of fights that the player has won against the boss.</description> </item>
	/// <item> <term>Deaths</term> <description>The total amount of deaths a player has experienced while fighting the boss.</description> </item>
	/// <item> <term>Attempts</term> <description>The amount of fights a player has started against the boss, win or loss.</description> </item>
	/// <item> <term>Play Time First</term> <description>The amount of play time that has passed up until the first kill of the boss.</description> </item>
	/// </list>
	/// <para>[Records]</para>
	/// <list type="bullet">
	/// <item> <term>Duration</term> <description>The amount of time it took to defeat the boss.</description> </item>
	/// <item> <term>HitsTaken</term> <description>The amount of times a player has taken damage while fighting the boss.</description> </item>
	/// </list>
	/// </summary>
	public class PersonalStats : TagSerializable
	{
		/// Statistics
		public int kills;
		public int deaths;
		public int attempts;
		public long playTimeFirst = -1;

		/// Records
		public int durationPrev = -1;
		public int durationBest = -1;
		public int durationPrevBest = -1;
		public int durationFirst = -1;

		public int hitsTakenPrev = -1;
		public int hitsTakenBest = -1;
		public int hitsTakenPrevBest = -1;
		public int hitsTakenFirst = -1;

		public static Func<TagCompound, PersonalStats> DESERIALIZER = tag => new PersonalStats(tag);

		public PersonalStats() { }

		private PersonalStats(TagCompound tag) {
			kills = tag.Get<int>(nameof(kills));
			deaths = tag.Get<int>(nameof(deaths));
			attempts = tag.Get<int>(nameof(attempts));
			playTimeFirst = tag.Get<long>(nameof(playTimeFirst));

			durationPrev = tag.Get<int>(nameof(durationPrev));
			durationBest = tag.Get<int>(nameof(durationBest));
			durationPrevBest = tag.Get<int>(nameof(durationPrevBest));
			durationFirst = tag.Get<int>(nameof(durationFirst));

			hitsTakenPrev = tag.Get<int>(nameof(hitsTakenPrev));
			hitsTakenBest = tag.Get<int>(nameof(hitsTakenBest));
			hitsTakenPrevBest = tag.Get<int>(nameof(hitsTakenPrevBest));
			hitsTakenFirst = tag.Get<int>(nameof(hitsTakenFirst));
		}

		public TagCompound SerializeData() {
			return new TagCompound {
				{ nameof(kills), kills },
				{ nameof(deaths), deaths },
				{ nameof(attempts), attempts },
				{ nameof(playTimeFirst), playTimeFirst },

				{ nameof(durationPrev), durationPrev },
				{ nameof(durationBest), durationBest },
				{ nameof(durationPrevBest), durationPrevBest },
				{ nameof(durationFirst), durationFirst },

				{ nameof(hitsTakenPrev), hitsTakenPrev },
				{ nameof(hitsTakenBest), hitsTakenBest },
				{ nameof(hitsTakenPrevBest), hitsTakenPrevBest },
				{ nameof(hitsTakenFirst), hitsTakenFirst },
			};
		}

		internal void NetSend(BinaryWriter writer, NetRecordID recordType) {
			writer.Write((int)recordType); // Write the record type(s) we are changing as NetRecieve will need to read this value.

			// If the record type is a reset, nothing else needs to be done, as the records will be wiped. Otherwise...
			if (!recordType.HasFlag(NetRecordID.ResettingRecord)) {
				// ...previous records are always overwritten for the player to view...
				writer.Write(durationPrev);
				writer.Write(hitsTakenPrev);

				// ... and any first or new records we set will be flagged for sending
				if (recordType.HasFlag(NetRecordID.PersonalBest_Duration)) {
					writer.Write(durationBest);
					writer.Write(durationPrevBest);
				}

				if (recordType.HasFlag(NetRecordID.PersonalBest_HitsTaken)) {
					writer.Write(hitsTakenBest);
					writer.Write(hitsTakenPrevBest);
				}

				if (recordType.HasFlag(NetRecordID.FirstVictory)) {
					writer.Write(durationFirst);
					writer.Write(hitsTakenFirst);
				}
			}
		}

		internal void NetRecieve(BinaryReader reader) {
			NetRecordID recordType = (NetRecordID)reader.ReadInt32();
			if (recordType.HasFlag(NetRecordID.ResettingRecord)) {
				if (recordType.HasFlag(NetRecordID.FirstVictory_Reset)) {
					playTimeFirst = -1;
					durationFirst = hitsTakenFirst = -1;
				}

				if (recordType.HasFlag(NetRecordID.PersonalBest_Reset)) {
					durationBest = durationPrevBest = hitsTakenBest = hitsTakenPrevBest = -1;
				}
			}
			else {
				kills++; // Kills always increase by 1, since records will only be updated when a boss is defeated
				durationPrev = reader.ReadInt32();
				hitsTakenPrev = reader.ReadInt32();

				if (recordType.HasFlag(NetRecordID.PersonalBest_Duration)) {
					durationBest = reader.ReadInt32();
					durationPrevBest = reader.ReadInt32();
				}

				if (recordType.HasFlag(NetRecordID.PersonalBest_HitsTaken)) {
					hitsTakenBest = reader.ReadInt32();
					hitsTakenPrevBest = reader.ReadInt32();
				}

				if (recordType.HasFlag(NetRecordID.FirstVictory)) {
					durationFirst = reader.ReadInt32();
					hitsTakenFirst = reader.ReadInt32();
				}

				// This should always be read by Multiplayer clients, so create combat texts on new records
				if (recordType.HasFlag(NetRecordID.WorldRecord)) {
					CombatText.NewText(Main.LocalPlayer.getRect(), Color.LightYellow, "New Record!", true);
				}
				else if (recordType.HasFlag(NetRecordID.NewPersonalBest)) {
					CombatText.NewText(Main.LocalPlayer.getRect(), Color.LightYellow, "New World Record!", true);
				}
			}
		}

		/// <summary>
		/// Gets the personal kills and deaths of an entry record in a string format.
		/// If the designated boss has not been killed nor has killed a player, 'Unchallenged' will be returned instead.
		/// </summary>
		public string GetKDR() {
			if (kills == 0 && deaths == 0)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.Unchallenged");

			return $"{kills} {Language.GetTextValue($"{BossLogUI.LangLog}.Records.Kills")} / {deaths} {Language.GetTextValue($"{BossLogUI.LangLog}.Records.Deaths")}";
		}

		/// <summary>
		/// Gets the duration time of an entry record in a string format.
		/// If the designated boss has not been defeated yet, 'No Record' will be returned instead.
		/// </summary>
		/// <param name="ticks">The about of ticks a fight took.</param>
		/// <param name="sign">Only used when find a time difference using <see cref="TimeConversionDiff"/>.</param>
		public static string TimeConversion(int ticks, string sign = "") {
			if (ticks == -1)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.NoRecord");

			const int TicksPerSecond = 60;
			const int TicksPerMinute = TicksPerSecond * 60;
			int minutes = ticks / TicksPerMinute; // Minutes will still show if 0
			float seconds = (float)(ticks - (float)(minutes * TicksPerMinute)) / TicksPerSecond;
			return $"{sign}{minutes}:{seconds.ToString("00.00")}";
		}

		/// <summary>
		/// Takes a duration record and compares it against another.
		/// The result will be in a string format along with a symbol to represent the difference direction.
		/// </summary>
		/// <param name="recordTicks">The recorded amount of ticks that is being compared against.</param>
		/// <param name="compareTicks">The amount of ticks from the compare value.</param>
		/// <param name="diff">The color value that represents the record time difference. 
		///		<list type="bullet">
		///		<item><term>Red</term> <description>The time recorded is slower than the compare value (+)</description></item>
		///		<item><term>Yellow</term> <description>No difference between the record's time (±)</description></item>
		///		<item><term>Green</term> <description>The time recorded is faster than the compare value (-)</description></item></list>
		/// </param>
		public static string TimeConversionDiff(int recordTicks, int compareTicks, out Color diff) {
			if (recordTicks == -1 || compareTicks == -1) {
				diff = default;
				return ""; // records cannot be compared
			}

			// A color and sign should be picked
			int tickDiff = recordTicks - compareTicks;
			string sign;
			if (tickDiff > 0) {
				sign = "+";
				diff = Color.Red;
			}
			else if (tickDiff == 0) {
				sign = "±";
				diff = Color.Yellow;
			}
			else {
				tickDiff *= -1;
				sign = "-";
				diff = Color.Green;
			}

			return TimeConversion(tickDiff, sign);
		}

		/// <summary>
		/// Gets the hits taken entry record in a string format.
		/// If the user's record is zero, 'No Hit!' will be returned instead.
		/// Otherwise, if the entry has not been defeated yet, 'No Record' will be returned instead.
		/// </summary>
		/// <param name="count">The record being checked.</param>
		public static string HitCount(int count) {
			if (count == -1)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.NoRecord");
			
			if (count == 0)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.NoHit");
			
			return count.ToString();
		}

		/// <summary>
		/// Takes a Hits Taken record and compares it against another.
		/// The result will be in a string format along with a symbol to represent the difference direction.
		/// </summary>
		/// <param name="count">The recorded amount of hits that is being compared against.</param>
		/// <param name="compareCount">The amount of hits from the compare value.</param>
		/// <param name="diff">The color value that represents the record time difference. 
		///		<list type="bullet">
		///		<item><term>Red</term> <description>The time recorded is slower than the compare value (+)</description></item>
		///		<item><term>Yellow</term> <description>No difference between the record's time (±)</description></item>
		///		<item><term>Green</term> <description>The time recorded is faster than the compare value (-)</description></item></list>
		/// </param>
		public static string HitCountDiff(int count, int compareCount, out Color diff) {
			if (count == -1 || compareCount == -1) {
				diff = default;
				return ""; // records cannot be compared
			}

			// A color and sign should be picked
			int countDiff = count - compareCount;
			string sign;
			if (countDiff > 0) {
				sign = "+";
				diff = Color.Red;
			}
			else if (countDiff == 0) {
				sign = "±";
				diff = Color.Yellow;
			}
			else {
				countDiff *= -1;
				sign = "-";
				diff = Color.Green;
			}

			return $"{sign}{countDiff}";
		}

		/// <summary>
		/// Gets the recorded play time snapshot for the entry's first defeation in a string format.
		/// If the entry has not yet been defeated, 'Unchallenged' will be returned instead.
		/// </summary>
		public string PlayTimeToString() {
			if (kills == 0)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.Unchallenged");

			int hours = (int)(playTimeFirst / TimeSpan.TicksPerHour);
			int minutes = (int)((playTimeFirst - (hours * TimeSpan.TicksPerHour)) / TimeSpan.TicksPerMinute);
			float seconds = (float)((playTimeFirst - (float)(hours * TimeSpan.TicksPerHour) - (float)(minutes * TimeSpan.TicksPerMinute)) / TimeSpan.TicksPerSecond);
			return $"{(hours > 0 ? hours + ":" : "")}{(hours > 0 ? minutes.ToString("00") : minutes)}:{seconds.ToString("00.00")}";
		}
	}

	/* Plans for World Records
	 * All players that join a "world" are recorded to a list
	 * Server Host can remove anyone from this list (ex. Troll, wrong character join)
	 * Server grabs BEST Records from the list of players and determines which one is the best
	 */

	/// <summary>
	/// In multiplayer, players are able to set world records against other players.
	/// This will contain global kills and deaths as well as the best record's value and holder.
	/// </summary>
	public class WorldStats : TagSerializable
	{
		public int totalKills;
		public int totalDeaths;

		public List<string> durationHolder = new List<string> { };
		public int durationWorld = -1;
		
		public List<string> hitsTakenHolder = new List<string> { };
		public int hitsTakenWorld = -1;

		public bool DurationEmpty => durationHolder.Count == 0 || durationWorld == -1;
		public bool HitsTakenEmpty => hitsTakenHolder.Count == 0 || hitsTakenWorld == -1;

		public static Func<TagCompound, WorldStats> DESERIALIZER = tag => new WorldStats(tag);

		public WorldStats() { }

		private WorldStats(TagCompound tag) {
			totalKills = tag.Get<int>(nameof(totalKills));
			totalDeaths = tag.Get<int>(nameof(totalDeaths));

			durationHolder = tag.GetList<string>(nameof(durationHolder)).ToList();
			durationWorld = tag.Get<int>(nameof(durationWorld));

			hitsTakenHolder = tag.GetList<string>(nameof(hitsTakenHolder)).ToList();
			hitsTakenWorld = tag.Get<int>(nameof(hitsTakenWorld));
		}

		public TagCompound SerializeData() {
			return new TagCompound {
				{ nameof(totalKills), totalKills },
				{ nameof(totalDeaths), totalDeaths },

				{ nameof(durationHolder), durationHolder },
				{ nameof(durationWorld), durationWorld },

				{ nameof(hitsTakenHolder), hitsTakenHolder },
				{ nameof(hitsTakenWorld), hitsTakenWorld },
			};
		}

		internal void NetSend(BinaryWriter writer, NetRecordID netRecords) {
			writer.Write((int)netRecords); // Write the record type(s) we are changing. NetRecieve will need to read this value.

			// Packet should have any beaten record values and holders written on it
			if (netRecords.HasFlag(NetRecordID.WorldRecord_Duration)) {
				writer.Write(durationWorld);
				writer.Write(durationHolder.Count);
				foreach (string name in durationHolder) {
					writer.Write(name);
				}
			}

			if (netRecords.HasFlag(NetRecordID.WorldRecord_HitsTaken)) {
				writer.Write(hitsTakenWorld);
				writer.Write(hitsTakenHolder.Count);
				foreach (string name in hitsTakenHolder) {
					writer.Write(name);
				}
			}
		}

		internal void NetRecieve(BinaryReader reader) {
			NetRecordID netRecords = (NetRecordID)reader.ReadInt32(); // Read the type of record being updated
			totalKills++; // Kills always increase by 1, since records will only be updated when a boss is defeated
			//TODO: figure out death counts

			// Set the world record values and holders
			if (netRecords.HasFlag(NetRecordID.WorldRecord_Duration)) {
				durationWorld = reader.ReadInt32();
				int durationHolderTotal = reader.ReadInt32();
				durationHolder.Clear();
				for (int i = 0; i < durationHolderTotal; i++) {
					durationHolder.Add(reader.ReadString());
				}
			}

			if (netRecords.HasFlag(NetRecordID.WorldRecord_HitsTaken)) {
				hitsTakenWorld = reader.ReadInt32();
				int hitsTakenHolderTotal = reader.ReadInt32();
				hitsTakenHolder.Clear();
				for (int i = 0; i < hitsTakenHolderTotal; i++) {
					hitsTakenHolder.Add(reader.ReadString());
				}
			}
		}

		/// <summary>
		/// Gets the total kills and deaths of an entry in a string format.
		/// If the entry has not yet been defeated, 'Unchallenged' will be returned instead.
		/// </summary>
		public string GetGlobalKDR() {
			if (totalKills == 0 && totalDeaths == 0)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.Unchallenged");

			return $"{totalKills} {Language.GetTextValue($"{BossLogUI.LangLog}.Records.Kills")} / {totalDeaths} {Language.GetTextValue($"{BossLogUI.LangLog}.Records.Deaths")}";
		}

		/// <summary>
		/// Lists the current holders of the duration world record.
		/// If the entry has not yet been defated, 'Be the first to claim the world record!' will be returned instead.
		/// </summary>
		public string ListDurationRecordHolders() {
			if (DurationEmpty)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.ClaimRecord");

			string list = Language.GetTextValue($"{BossLogUI.LangLog}.Records.RecordHolder");
			foreach (string name in durationHolder) {
				list += $"\n •{name}";
			}
			return list;
		}

		/// <summary>
		/// Lists the current holders of the hits taken world record.
		/// If the entry has not yet been defated, 'Be the first to claim the world record!' will be returned instead.
		/// </summary>
		public string ListHitsTakenRecordHolders() {
			if (HitsTakenEmpty)
				return Language.GetTextValue($"{BossLogUI.LangLog}.Records.ClaimRecord");

			string list = Language.GetTextValue($"{BossLogUI.LangLog}.Records.RecordHolder");
			foreach (string name in hitsTakenHolder) {
				list += $"\n •{name}";
			}
			return list;
		}
	}
}

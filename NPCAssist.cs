﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace BossChecklist
{
	class NPCAssist : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		// When an entry NPC spawns, setup the world and player trackers for the upcoming fight
		public override void OnSpawn(NPC npc, IEntitySource source) {
			if (BossChecklist.DebugConfig.DISABLERECORDTRACKINGCODE) {
				return;
			}
			if (npc.realLife != -1 && npc.realLife != npc.whoAmI) {
				return; // Checks for multi-segmented bosses?
			}

			int index = GetBossInfoIndex(npc.type, true);
			if (index == -1) {
				return; // Make sure the npc is an entry
			}

			// If not marked active, set to active and reset trackers for all players to start tracking records for this fight
			if (!WorldAssist.Tracker_ActiveEntry[index]) {
				WorldAssist.Tracker_ActiveEntry[index] = true;
				for (int j = 0; j < Main.maxPlayers; j++) {
					if (!Main.player[j].active)
						continue; // skip any inactive players
					else
						WorldAssist.Tracker_StartingPlayers[index][j] = true; // Active players when the boss spawns will be counted

					// Reset Timers and counters so we can start recording the next fight
					PlayerAssist modPlayer = Main.player[j].GetModPlayer<PlayerAssist>();
					int recordIndex = BossChecklist.bossTracker.SortedBosses[index].GetRecordIndex;
					modPlayer.Tracker_Duration[recordIndex] = 0;
					modPlayer.Tracker_HitsTaken[recordIndex] = 0;
				}
			}
		}

		// Allow world trackers to update within the NPC
		public override void PostAI(NPC npc) {
			// Check to see if this npc is used within an BossInfo entry and a BossRecord entry
			int bossIndex = GetBossInfoIndex(npc.type, true);
			int recordIndex = BossChecklist.bossTracker.SortedBosses[bossIndex].GetRecordIndex;
			if (bossIndex == -1 || recordIndex == -1) {
				return;
			}

			// If marked as active we should...
			if (WorldAssist.Tracker_ActiveEntry[recordIndex]) {
				// ...remove any players that become inactive during the fight
				for (int i = 0; i < Main.maxPlayers; i++) {
					if (!Main.player[i].active) {
						WorldAssist.Tracker_StartingPlayers[recordIndex][i] = false;
					}
				}

				// ...check if the npc is actually still active or not and display a despawn message if they are no longer active (but not killed!)
				if (FullyInactive(npc, bossIndex)) {
					WorldAssist.Tracker_ActiveEntry[recordIndex] = false; // No longer an active boss (only other time this is set to false is NPC.OnKill)
					string message = GetDespawnMessage(npc, bossIndex);
					if (message != "") {
						if (Main.netMode == NetmodeID.SinglePlayer) {
							Main.NewText(Language.GetTextValue(message, npc.FullName), Colors.RarityPurple);
						}
						else {
							ChatHelper.BroadcastChatMessage(NetworkText.FromKey(message, npc.FullName), Colors.RarityPurple);
						}
					}
				}
			}
		}

		// When an NPC is killed and fully inactive, the fight has ended, stopping record trackers
		public override void OnKill(NPC npc) {
			HandleDownedNPCs(npc); // Custom downed bool code
			SendEntryMessage(npc); // Display a message for Limbs/Towers if config is enabled

			// Stop record trackers and record them to the player while also checking for records and world records
			if (!BossChecklist.DebugConfig.DISABLERECORDTRACKINGCODE) {
				int index = GetBossInfoIndex(npc.type, true);
				if (index != -1) {
					if (FullyInactive(npc, index)) {
						if (!BossChecklist.DebugConfig.NewRecordsDisabled && !BossChecklist.DebugConfig.RecordTrackingDisabled) {
							if (Main.netMode == NetmodeID.SinglePlayer) {
								CheckRecords(npc, index);
							}
							else if (Main.netMode == NetmodeID.Server) {
								CheckRecordsMultiplayer(npc, index);
							}
						}
						if (BossChecklist.DebugConfig.ShowInactiveBossCheck) {
							Main.NewText(npc.FullName + ": " + FullyInactive(npc, index));
						}
						WorldAssist.worldRecords[BossChecklist.bossTracker.SortedBosses[index].GetRecordIndex].stats.totalKills++;

						// Reset world variables after record checking takes place
						WorldAssist.Tracker_ActiveEntry[index] = false;
						WorldAssist.Tracker_StartingPlayers[index] = new bool[Main.maxPlayers];
					}
				}
			}
		}

		/// <summary>
		/// Loops through all entries in BossTracker.SortedBosses to find BossInfo that contains the specified npc type.
		/// </summary>
		/// <param name="bossesOnly">Leave false to use this for any entry. Set to true while using this for boss record purposes.</param>
		/// <returns>The index within BossTracker.SortedBosses. Returns -1 if searching for an invalid npc type.</returns>
		public static int GetBossInfoIndex(int npcType, bool bossesOnly = false) {
			if (!BossChecklist.bossTracker.BossCache[npcType])
				return -1;

			List<BossInfo> BossInfoList = BossChecklist.bossTracker.SortedBosses;
			for (int index = 0; index < BossInfoList.Count; index++) {
				if (bossesOnly && BossInfoList[index].type != EntryType.Boss) {
					continue;
				}
				if (BossInfoList[index].npcIDs.Contains(npcType)) {
					return index;
				}
			}
			return -1;
		}

		/// <summary>
		/// Searches for all npc types of a given SortedBosses index and checks their active status within Main.npc.
		/// </summary>
		/// <returns>Whether or not an npc listed in the specified entry's npc pool is active or not.</returns>
		public static bool FullyInactive(NPC npc, int index) {
			// Check all multibosses to see if the NPC is truly dead
			// index should be checked for a value of -1 before submitting, but just in case...
			if (index == -1)
				return !npc.active;

			// Loop through the npc types of the given index and check if any are currently an active npc
			foreach (int bossType in BossChecklist.bossTracker.SortedBosses[index].npcIDs) {
				if (Main.npc.Any(nPC => nPC != npc && nPC.active && nPC.type == bossType))
					return false;
			}

			// If none of the npc types are active, return the NPC's own active state
			return !npc.active;
		}

		/// <summary>
		/// Uses an entry's custom despawn message logic to determine what string or localization key should be sent.
		/// </summary>
		/// <returns>The despawn message of the provided npc.</returns>
		public string GetDespawnMessage(NPC npc, int index) {
			if (npc.life <= 0) {
				return ""; // If the boss was killed, don't display a despawn message
			}

			string messageType = BossChecklist.ClientConfig.DespawnMessageType;

			// Provide the npc for the custom message
			// If null or empty, give a generic message instead of a custom one
			if (messageType == "Unique") {
				string customMessage = BossChecklist.bossTracker.SortedBosses[index].customDespawnMessages(npc);
				if (!string.IsNullOrEmpty(customMessage)) {
					return customMessage;
				}
			}

			// If the Unique message was empty or the player is using Generic despawn messages, try to find an appropriate despawn message to send
			// Return a generic despawn message is any player is left alive or return a boss victory despawn message if all player's were killed
			if (messageType != "Disabled") {
				return Main.player.Any(plr => plr.active && !plr.dead) ? "Mods.BossChecklist.BossDespawn.Generic" : "Mods.BossChecklist.BossVictory.Generic";
			}
			// The despawn message feature was disabled. Return an empty message.
			return "";
		}

		/// <summary>
		/// Takes the data from record trackers and updates the player's saved records accordingly.
		/// <para>Only runs in the Singleplayer netmode.</para>
		/// </summary>
		public void CheckRecords(NPC npc, int bossIndex) {
			// Player must have contributed to the boss fight
			if (!npc.playerInteraction[Main.myPlayer]) {
				return;
			}

			PlayerAssist modPlayer = Main.LocalPlayer.GetModPlayer<PlayerAssist>();
			bool newRecordSet = false;
			int recordIndex = BossChecklist.bossTracker.SortedBosses[bossIndex].GetRecordIndex;
			PersonalStats bossStats = modPlayer.RecordsForWorld[recordIndex].stats;

			int durationAttempt = modPlayer.Tracker_Duration[recordIndex];
			int currentBestDuration = bossStats.durationBest;
			
			int hitsTakenAttempt = modPlayer.Tracker_HitsTaken[recordIndex];
			int currentBestHitsTaken = bossStats.hitsTakenBest;

			bossStats.kills++; // Kills always go up, since comparing only occurs if boss was defeated

			// If the player has beaten their best record, we change BEST to PREV and make the current attempt the new BEST
			// Otherwise, just overwrite PREV with the current attempt
			if (durationAttempt < currentBestDuration || currentBestDuration == -1) {
				// New Record should not appear on first boss kill, which would appear as -1
				if (bossStats.durationBest != -1) {
					newRecordSet = true;
				}
				bossStats.durationPrev = currentBestDuration;
				bossStats.durationBest = durationAttempt;
			}
			else {
				bossStats.durationPrev = durationAttempt;
			}

			// Empty check should be less than 0 because 0 is achievable (No Hit)
			if (hitsTakenAttempt < currentBestHitsTaken || currentBestHitsTaken == -1) {
				if (bossStats.hitsTakenBest != -1) {
					newRecordSet = true;
				}
				bossStats.hitsTakenPrev = currentBestHitsTaken;
				bossStats.hitsTakenBest = hitsTakenAttempt;
			}
			else {
				bossStats.hitsTakenPrev = hitsTakenAttempt;
			}

			// If a new record was made, notify the player
			// This will not show for newly set records
			if (newRecordSet) {
				modPlayer.hasNewRecord[bossIndex] = true;
				// Compare records to World Records. Logically, you can only beat the world records if you have beaten your own record
				// TODO: Move World Record texts to Multiplayer exclusively. Check should still happen.
				string recordType = "Mods.BossChecklist.BossLog.Terms.";
				recordType += CheckWorldRecords(recordIndex) ? "NewWorldRecord" : "NewRecord";
				string message = Language.GetTextValue(recordType);
				CombatText.NewText(Main.LocalPlayer.getRect(), Color.LightYellow, message, true);
			}
		}

		// TODO: update method to improve what it needs to do
		/// <summary>
		/// Takes the record data from all players and updates them where needed.
		/// <para>Only runs in the ??? netmode.</para>
		/// </summary>
		public void CheckRecordsMultiplayer(NPC npc, int bossIndex) {
			int recordIndex = BossChecklist.bossTracker.SortedBosses[bossIndex].GetRecordIndex;
			WorldStats worldRecords = WorldAssist.worldRecords[recordIndex].stats;
			string[] newRecordHolders = new string[] { "", "" };
			int[] newWorldRecords = new int[]{
				worldRecords.durationWorld,
				worldRecords.hitsTakenWorld
			};
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];

				// Players must be active AND have interacted with the boss AND cannot have recordingstats disabled
				if (!player.active || !npc.playerInteraction[i]) {
					continue;
				}
				PersonalStats serverRecord = BossChecklist.ServerCollectedRecords[i][recordIndex].stats;
				PlayerAssist modPlayer = player.GetModPlayer<PlayerAssist>();
				int durationNew = modPlayer.Tracker_Duration[recordIndex];
				int hitsTakenNew = modPlayer.Tracker_HitsTaken[recordIndex];

				RecordID recordType = RecordID.None;
				// For each record type we check if its beats the current record or if it is not set already
				// If it is beaten, we add a flag to specificRecord to allow newRecord's numbers to override the current record
				if (durationNew < serverRecord.durationBest || serverRecord.durationBest <= 0) {
					Console.WriteLine($"{player.name} set a new record for DURATION: {durationNew} (Previous Record: {serverRecord.durationBest})");
					recordType |= RecordID.Duration;
					serverRecord.durationPrev = serverRecord.durationBest;
					serverRecord.durationBest = durationNew;
				}
				else {
					serverRecord.durationPrev = durationNew;
				}

				if (hitsTakenNew < serverRecord.hitsTakenBest || serverRecord.hitsTakenBest < 0) {
					Console.WriteLine($"{player.name} set a new record for HITS TAKEN: {hitsTakenNew} (Previous Record: {serverRecord.hitsTakenBest})");
					recordType |= RecordID.HitsTaken;
					serverRecord.hitsTakenPrev = serverRecord.hitsTakenBest;
					serverRecord.hitsTakenBest = hitsTakenNew;
				}
				else {
					serverRecord.hitsTakenPrev = hitsTakenNew;
				}
				
				// Make and send the packet
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)PacketMessageType.RecordUpdate);
				packet.Write((int)recordIndex); // Which boss record are we changing?
				modPlayer.RecordsForWorld[recordIndex].stats.NetSend(packet, recordType); // Writes all the variables needed
				packet.Send(toClient: i); // Server --> Multiplayer client // We send to the player as only they need to see their own records
			}
			if (newRecordHolders.Any(x => x != "")) {
				RecordID specificRecord = RecordID.None;
				if (newRecordHolders[0] != "") {
					specificRecord |= RecordID.Duration;
					worldRecords.durationHolder = newRecordHolders[0];
					worldRecords.durationWorld = newWorldRecords[0];
				}
				if (newRecordHolders[1] != "") {
					specificRecord |= RecordID.HitsTaken;
					worldRecords.hitsTakenHolder = newRecordHolders[1];
					worldRecords.hitsTakenWorld = newWorldRecords[1];
				}
				
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)PacketMessageType.WorldRecordUpdate);
				packet.Write((int)recordIndex); // Which boss record are we changing?
				worldRecords.NetSend(packet, specificRecord);
				packet.Send(); // Server --> Server (world data for everyone)
			}
		}

		// TODO: update method to be compatible with both CheckRecords and CheckRecordsMultiplayer
		/// <summary>
		/// Takes the world records with the updated player record data, updating the world records if they were beaten.
		/// </summary>
		/// <returns>Whether or not the world record was beaten</returns>
		public bool CheckWorldRecords(int recordIndex) { // Returns whether or not to stop the New Record! text from appearing to show World Record! instead
			Player player = Main.LocalPlayer;
			PersonalStats playerRecord = player.GetModPlayer<PlayerAssist>().RecordsForWorld[recordIndex].stats;
			WorldStats worldRecord = WorldAssist.worldRecords[recordIndex].stats;
			bool newRecord = false;

			if (playerRecord.durationBest < worldRecord.durationWorld || worldRecord.durationWorld <= 0) {
				// only say World Record if you the player is on a server OR if the player wasn't holding the previoes record
				newRecord = (worldRecord.durationHolder != player.name && worldRecord.durationHolder != "") || Main.netMode == NetmodeID.MultiplayerClient;
				worldRecord.durationWorld = playerRecord.durationBest;
				worldRecord.durationHolder = player.name;
			}
			if (playerRecord.hitsTakenBest < worldRecord.hitsTakenWorld || worldRecord.hitsTakenWorld < 0) {
				newRecord = (worldRecord.hitsTakenHolder != player.name && worldRecord.hitsTakenHolder != "") || Main.netMode == NetmodeID.MultiplayerClient;
				worldRecord.hitsTakenWorld = playerRecord.hitsTakenBest;
				worldRecord.hitsTakenHolder = player.name;
			}
			return newRecord;
		}

		// All of BossChecklist's custom downed variables will be handled here
		public void HandleDownedNPCs(NPC npc) {
			if ((npc.type == NPCID.DD2DarkMageT1 || npc.type == NPCID.DD2DarkMageT3) && !WorldAssist.downedDarkMage) {
				WorldAssist.downedDarkMage = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
			else if ((npc.type == NPCID.DD2OgreT2 || npc.type == NPCID.DD2OgreT3) && !WorldAssist.downedOgre) {
				WorldAssist.downedOgre = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
			else if (npc.type == NPCID.PirateShip && !WorldAssist.downedFlyingDutchman) {
				WorldAssist.downedFlyingDutchman = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
			else if (npc.type == NPCID.MartianSaucerCore && !WorldAssist.downedMartianSaucer) {
				WorldAssist.downedMartianSaucer = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
		}

		// Depending on what configs are enabled, this will send messages in chat displaying what NPC has been defeated
		public void SendEntryMessage(NPC npc) {
			if (NPCisLimb(npc)) {
				if (!BossChecklist.ClientConfig.LimbMessages)
					return;

				string partName = npc.GetFullNetName().ToString();
				if (npc.type == NPCID.SkeletronHand) {
					partName = "Skeletron Hand"; // TODO: Localization needed
				}
				string defeatedLimb = "Mods.BossChecklist.BossDefeated.Limb";
				if (Main.netMode == NetmodeID.SinglePlayer) {
					Main.NewText(Language.GetTextValue(defeatedLimb, partName), Colors.RarityGreen);
				}
				else {
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(defeatedLimb, partName), Colors.RarityGreen);
				}
			}
			else if (npc.type == NPCID.LunarTowerSolar || npc.type == NPCID.LunarTowerVortex || npc.type == NPCID.LunarTowerNebula || npc.type == NPCID.LunarTowerStardust) {
				if (!BossChecklist.ClientConfig.PillarMessages)
					return;
				string defeatedTower = "Mods.BossChecklist.BossDefeated.Tower";
				string npcName = npc.GetFullNetName().ToString();
				if (Main.netMode == NetmodeID.SinglePlayer) {
					Main.NewText(Language.GetTextValue(defeatedTower, npcName), Colors.RarityPurple);
				}
				else {
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(defeatedTower, npcName), Colors.RarityPurple);
				}
			}
		}

		// TODO: Expand on this idea for modded entries?
		public bool NPCisLimb(NPC npcType) {
			int[] limbNPCs = new int[] {
				NPCID.PrimeSaw,
				NPCID.PrimeLaser,
				NPCID.PrimeCannon,
				NPCID.PrimeVice,
				NPCID.SkeletronHand,
				NPCID.GolemFistLeft,
				NPCID.GolemFistRight,
				NPCID.GolemHead
			};

			bool isTwinsRet = npcType.type == NPCID.Retinazer && Main.npc.Any(x => x.type == NPCID.Spazmatism && x.active);
			bool isTwinsSpaz = npcType.type == NPCID.Spazmatism && Main.npc.Any(x => x.type == NPCID.Retinazer && x.active);

			return limbNPCs.Contains(npcType.type) || isTwinsRet || isTwinsSpaz;
		}
	}
}

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
		public const string LangChat = "Mods.BossChecklist.ChatMessages";

		// When an entry NPC spawns, setup the world and player trackers for the upcoming fight
		public override void OnSpawn(NPC npc, IEntitySource source) {
			// Only single player and server should be starting the record tracking process
			if (Main.netMode == NetmodeID.MultiplayerClient || BossChecklist.DebugConfig.DISABLERECORDTRACKINGCODE)
				return;

			EntryInfo entry = GetEntryInfo(npc.type);
			if (entry == null)
				return; // Make sure the npc is an entry

			// If not marked active, set to active and reset trackers for all players to start tracking records for this fight
			int recordIndex = entry.GetRecordIndex;
			if (!WorldAssist.Tracker_ActiveEntry[recordIndex]) {
				WorldAssist.Tracker_ActiveEntry[recordIndex] = true;

				if (Main.netMode == NetmodeID.SinglePlayer) {
					WorldAssist.Tracker_StartingPlayers[recordIndex, Main.LocalPlayer.whoAmI] = true; // Active players when the boss spawns will be counted
					PlayerAssist modPlayer = Main.LocalPlayer.GetModPlayer<PlayerAssist>();
					modPlayer.Tracker_Duration[recordIndex] = 0;
					modPlayer.Tracker_HitsTaken[recordIndex] = 0;
				}
				else if (Main.netMode == NetmodeID.Server) {
					foreach (Player player in Main.player) {
						if (!player.active)
							continue; // skip any inactive players

						WorldAssist.Tracker_StartingPlayers[recordIndex, player.whoAmI] = true; // Active players when the boss spawns will be counted

						// This is updated serverside
						PlayerAssist modPlayer = player.GetModPlayer<PlayerAssist>();
						modPlayer.Tracker_Duration[recordIndex] = 0;
						modPlayer.Tracker_HitsTaken[recordIndex] = 0;

						// Needs to be updated client side as well
						// Send packets from the server to all participating players to reset their trackers for the recordIndex provided
						ModPacket packet = Mod.GetPacket();
						packet.Write((byte)PacketMessageType.ResetTrackers);
						packet.Write(recordIndex);
						packet.Write(player.whoAmI);
						packet.Send(toClient: player.whoAmI); // Server --> Multiplayer client
					}
				}
			}
		}

		// When an NPC is killed and fully inactive the fight has ended, so stop all record trackers
		public override void OnKill(NPC npc) {
			HandleDownedNPCs(npc.type); // Custom downed bool code
			SendEntryMessage(npc); // Display a message for Limbs/Towers if config is enabled

			if (BossChecklist.DebugConfig.DISABLERECORDTRACKINGCODE)
				return;

			// Stop record trackers and record them to the player while also checking for records and world records
			
			EntryInfo entry = GetEntryInfo(npc.type);
			if (entry != null) {
				int index = entry.GetIndex;
				int recordIndex = entry.GetRecordIndex;
				if (FullyInactive(npc, index, true)) {
					if (!BossChecklist.DebugConfig.RecordTrackingDisabled) {
						if (Main.netMode == NetmodeID.Server) {
							Networking.UpdateServerRecords(npc, recordIndex);
						}
						else if (Main.netMode == NetmodeID.SinglePlayer) {
							CheckRecords(npc, recordIndex);
						}
						else if (Main.netMode == NetmodeID.MultiplayerClient) {
							Networking.SubmitPlayTimeToServer(npc, recordIndex);
						}
					}

					if (BossChecklist.DebugConfig.ShowInactiveBossCheck && Main.netMode != NetmodeID.Server)
						Main.NewText(npc.FullName + ": " + FullyInactive(npc, index));

					WorldAssist.worldRecords[recordIndex].stats.totalKills++;

					// Reset world variables after record checking takes place
					WorldAssist.Tracker_ActiveEntry[recordIndex] = false;
					for (int i = 0; i < Main.maxPlayers; i++) {
						WorldAssist.Tracker_StartingPlayers[recordIndex, i] = false;
					}
				}
			}
		}

		/// <summary>
		/// Loops through all entries in BossTracker.SortedEntries to find EntryInfo that contains the specified npc type.
		/// This method is mainly used for boss record purposes.
		/// </summary>
		/// <returns>A valid EntryInfo entry within the registered entries. Returns null if no entry can be found.</returns>
		public static EntryInfo GetEntryInfo(int npcType) {
			if (!BossChecklist.bossTracker.EntryCache[npcType])
				return null; // the entry hasn't been registered

			foreach (EntryInfo entry in BossChecklist.bossTracker.SortedEntries) {
				if (entry.type == EntryType.Boss && entry.npcIDs.Contains(npcType))
					return entry; // if the npc pool contains the npc type, return the current the index
			}

			return null; // no entry found
		}

		/// <summary>
		/// Searches for all npc types of a given SortedBosses index and checks their active status within Main.npc. Does not include the passed NPC itself unless specified.
		/// </summary>
		/// <param name="excludePassedNPC">Whether or not this method should also check if the NPC passed is inactive.</param>
		/// <returns>Whether or not an npc listed in the specified entry's npc pool is active or not.</returns>
		public static bool FullyInactive(NPC npc, int index, bool excludePassedNPC = false) {
			if (index == -1)
				return !npc.active; // index should already be valid before submitting, but just in case return the NPC's active status

			foreach (int npcType in BossChecklist.bossTracker.SortedEntries[index].npcIDs) {
				if (Main.npc.Any(nPC => nPC != npc && nPC.active && nPC.type == npcType))
					return false; // if another npc within the same npc pool exists, the entry isn't truly inactive. Reminder: Boss minions should not submitted into NPC pools.
			}

			if (excludePassedNPC)
				return true; // If excluding the passed NPC from the active check, this should return true

			return !npc.active; // otherwise, return the NPC's active status
		}

		/// <summary>
		/// Determines what despawn message should be used based on client configuration and submitted entry info.
		/// </summary>
		/// <returns>A string or key of the despawn message of the passed npc. Returns null if no message can be found.</returns>
		public static string GetDespawnMessage(NPC npc, int index) {
			if (npc.life <= 0)
				return null; // If the boss was killed, don't display a despawn message

			string messageType = BossChecklist.ClientConfig.DespawnMessageType;
			if (messageType == "Unique") {
				// When unique despawn messages are enabled, pass the NPC for the custom message function provided by the entry
				string customMessage = BossChecklist.bossTracker.SortedEntries[index].customDespawnMessages(npc);
				if (!string.IsNullOrEmpty(customMessage))
					return customMessage; // this will only return a unique message if the custom message function properly assigns one
			}

			if (messageType != "Disabled") {
				// If the Unique message was empty/null or the player is using Generic despawn messages, try to find an appropriate despawn message to send
				// Return a generic despawn message if any player is left alive or return a boss victory despawn message if all player's were killed
				return Main.player.Any(plr => plr.active && !plr.dead) ? $"{LangChat}.Despawn.Generic" : $"{LangChat}.Loss.Generic";
			}
			// The despawn message feature was disabled. Return an empty message.
			return null;
		}

		/// <summary>
		/// Takes the data from record trackers and updates the player's saved records accordingly.
		/// <para>Only runs in the Singleplayer netmode.</para>
		/// </summary>
		private void CheckRecords(NPC npc, int recordIndex) {
			if (Main.netMode != NetmodeID.SinglePlayer)
				return;

			// Player must have contributed to the boss fight
			if (!npc.playerInteraction[Main.myPlayer] || !WorldAssist.Tracker_StartingPlayers[recordIndex, Main.myPlayer])
				return;

			PlayerAssist modPlayer = Main.LocalPlayer.GetModPlayer<PlayerAssist>();
			PersonalStats statistics = modPlayer.RecordsForWorld[recordIndex].stats;
			int trackedDuration = modPlayer.Tracker_Duration[recordIndex];
			int trackedHitsTaken = modPlayer.Tracker_HitsTaken[recordIndex];
			bool newRecordSet = false;

			statistics.kills++; // Kills always go up, since record checking only occurs if boss was defeated

			// If this was the first record made for the boss, set add them to the recordType
			if (statistics.durationFirst == -1 && statistics.hitsTakenFirst == -1) {
				statistics.durationFirst = trackedDuration;
				statistics.hitsTakenFirst = trackedHitsTaken;
				statistics.playTimeFirst = Main.ActivePlayerFileData.GetPlayTime().Ticks;
			}

			// Check if the tracked duration was better than the current best OR if the current best has not yet been achieved
			// Overwrite PrevBest records with the 'current' one first
			// If the current Best is -1, it was a first record, which means there was no prevBest (logic still works!)
			statistics.durationPrev = trackedDuration;
			if (trackedDuration < statistics.durationBest || statistics.durationBest == -1) {
				statistics.durationPrevBest = statistics.durationBest;
				statistics.durationBest = trackedDuration;
				if (statistics.durationBest != -1) {
					newRecordSet = true; // New Record should not appear above the player on the first record achieved
				}
			}

			// Repeat the same logic with the Hits Taken record
			statistics.hitsTakenPrev = trackedHitsTaken;
			if (trackedHitsTaken < statistics.hitsTakenBest || statistics.hitsTakenBest == -1) {
				statistics.hitsTakenPrevBest = statistics.hitsTakenBest;
				statistics.hitsTakenBest = trackedHitsTaken;
				if (statistics.hitsTakenBest != -1) {
					newRecordSet = true;
				}
			}

			// If a new record was made, notify the player. Again, this will not show for newly set records
			if (newRecordSet) {
				modPlayer.hasNewRecord[recordIndex] = true;
				// Compare records to World Records. Players must have beaten their own records to beat a world record
				string recordSet = CheckWorldRecords(recordIndex) ? "NewWorldRecord" : "NewRecord";
				string message = Language.GetTextValue($"{BossLogUI.LangLog}.Records.{recordSet}");
				CombatText.NewText(Main.LocalPlayer.getRect(), Color.LightYellow, message, true);
			}
		}

		/// <summary>
		/// Compares the updated player record data with the current world records and updating them if the records were beaten.
		/// <para>Only runs in the Singleplayer netmode.</para>
		/// </summary>
		/// <returns>Whether or not a world record was beaten or matched</returns>
		private bool CheckWorldRecords(int recordIndex) {
			if (BossChecklist.DebugConfig.DisableWorldRecords)
				return false;

			Player player = Main.LocalPlayer;
			PersonalStats playerRecord = player.GetModPlayer<PlayerAssist>().RecordsForWorld[recordIndex].stats;
			WorldStats worldRecords = WorldAssist.worldRecords[recordIndex].stats;
			bool newWorldRecord = false;

			// World records should NOT update if the world record is empty and the user is in SinglePlayer
			if (!worldRecords.DurationEmpty && playerRecord.durationBest <= worldRecords.durationWorld) {
				// If the world record was beaten, clear the list entirely
				if (playerRecord.durationBest < worldRecords.durationWorld) {
					worldRecords.durationHolder.Clear(); 
				}
				// Add the player name if the list does not contain it
				if (!worldRecords.durationHolder.Contains(player.name)) {
					worldRecords.durationHolder.Add(player.name);
				}
				worldRecords.durationWorld = playerRecord.durationBest;
				newWorldRecord = true;
			}
			if (!worldRecords.HitsTakenEmpty && playerRecord.hitsTakenBest <= worldRecords.hitsTakenWorld) {
				// If the world record was beaten, clear the list entirely
				if (playerRecord.hitsTakenBest < worldRecords.hitsTakenWorld) {
					worldRecords.hitsTakenHolder.Clear();
				}
				// Add the player name if the list does not contain it
				if (!worldRecords.hitsTakenHolder.Contains(player.name)) {
					worldRecords.hitsTakenHolder.Add(player.name);
				}
				worldRecords.hitsTakenWorld = playerRecord.hitsTakenBest;
				newWorldRecord = true;
			}
			return newWorldRecord; // Will be used to display CombatTexts of "New Record!" or "New World Record!"
		}

		/// <summary>
		/// Handles all of BossChecklist's custom downed variables, makring them as defeated and updating all clients when needed.
		/// </summary>
		public void HandleDownedNPCs(int npcType) {
			if ((npcType == NPCID.DD2DarkMageT1 || npcType == NPCID.DD2DarkMageT3) && !WorldAssist.downedDarkMage) {
				WorldAssist.downedDarkMage = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
			else if ((npcType == NPCID.DD2OgreT2 || npcType == NPCID.DD2OgreT3) && !WorldAssist.downedOgre) {
				WorldAssist.downedOgre = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
			else if (npcType == NPCID.PirateShip && !WorldAssist.downedFlyingDutchman) {
				WorldAssist.downedFlyingDutchman = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
			else if (npcType == NPCID.MartianSaucerCore && !WorldAssist.downedMartianSaucer) {
				WorldAssist.downedMartianSaucer = true;
				if (Main.netMode == NetmodeID.Server) {
					NetMessage.SendData(MessageID.WorldData);
				}
			}
		}

		/// <summary>
		/// Handles the extra npc defeation messages related to boss limbs and towers.
		/// These messages will not appear if the related configs are disabled.
		/// </summary>
		public void SendEntryMessage(NPC npc) {
			if (NPCisLimb(npc)) {
				if (!BossChecklist.ClientConfig.LimbMessages)
					return;

				// Skeletron's hands just use Skeletron's name instead of their own, so a custom name is needed
				string partName = npc.type == NPCID.SkeletronHand ? Lang.GetItemNameValue(ItemID.SkeletronHand) : npc.GetFullNetName().ToString();
				string defeatedLimb = $"{LangChat}.Defeated.Limb";
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

				string defeatedTower = $"{LangChat}.Defeated.Tower";
				string npcName = npc.GetFullNetName().ToString();
				if (Main.netMode == NetmodeID.SinglePlayer) {
					Main.NewText(Language.GetTextValue(defeatedTower, npcName), Colors.RarityPurple);
				}
				else {
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(defeatedTower, npcName), Colors.RarityPurple);
				}
			}
		}

		// This feature will not be extended to modded entries as those mods can handle limb messages themselves with ease, if desired.
		/// <summary>
		/// A 'limb' NPC is a part of a boss that is an extension of the boss, such as Skeletron's hands.
		/// This also considers boss's that are multiple entities, such as the Twins consisting of Retinazer and Spazmatism.
		/// </summary>
		/// <returns>Whether or not the npc is considered a 'limb'.</returns>
		public bool NPCisLimb(NPC npc) {
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

			bool isTwinsRet = npc.type == NPCID.Retinazer && Main.npc.Any(x => x.type == NPCID.Spazmatism && x.active);
			bool isTwinsSpaz = npc.type == NPCID.Spazmatism && Main.npc.Any(x => x.type == NPCID.Retinazer && x.active);

			return limbNPCs.Contains(npc.type) || isTwinsRet || isTwinsSpaz;
		}
	}
}

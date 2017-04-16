﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.UI.Chat;
using Terraria.ModLoader;

namespace BossChecklist.UI
{
	class BossChecklistUI : UIState
	{
		public UIHoverImageButton toggleButton;
		public UIPanel checklistPanel;
		public UIList checklistList;

		float spacing = 8f;
		public static bool visible = false;
		public static bool showCompleted = true;
		public static string hoverText = "";

		public override void OnInitialize()
		{
			checklistPanel = new UIPanel();
			checklistPanel.SetPadding(10);
			checklistPanel.Left.Pixels = 0;
			checklistPanel.HAlign = 1f;
			checklistPanel.Top.Set(50f, 0f);
			checklistPanel.Width.Set(250f, 0f);
			checklistPanel.Height.Set(-100, 1f);
			checklistPanel.BackgroundColor = new Color(73, 94, 171);

			toggleButton = new UIHoverImageButton(Main.itemTexture[ItemID.SuspiciousLookingEye], "Toggle Completed");
			toggleButton.OnClick += ToggleButtonClicked;
			toggleButton.Left.Pixels = spacing;
			toggleButton.Top.Pixels = spacing;
			checklistPanel.Append(toggleButton);

			checklistList = new UIList();
			checklistList.Top.Pixels = 32f + spacing;
			checklistList.Width.Set(-25f, 1f);
			checklistList.Height.Set(-32f, 1f);
			checklistList.ListPadding = 12f;
			checklistPanel.Append(checklistList);

			FixedUIScrollbar checklistListScrollbar = new FixedUIScrollbar();
			checklistListScrollbar.SetView(100f, 1000f);
			//checklistListScrollbar.Height.Set(0f, 1f);
			checklistListScrollbar.Top.Pixels = 32f + spacing;
			checklistListScrollbar.Height.Set(-32f - spacing, 1f);
			checklistListScrollbar.HAlign = 1f;
			checklistPanel.Append(checklistListScrollbar);
			checklistList.SetScrollbar(checklistListScrollbar);

			// Checklistlist populated when the panel is shown: UpdateCheckboxes()

			Append(checklistPanel);

			// TODO, game window resize issue
		}

		private void ToggleButtonClicked(UIMouseEvent evt, UIElement listeningElement)
		{
			Main.PlaySound(10, -1, -1, 1);
			showCompleted = !showCompleted;
			UpdateCheckboxes();
		}

		/*public bool ThoriumModDownedScout
		{
			get { return ThoriumMod.ThoriumWorld.downedScout; }
		}
		public bool CalamityDS => CalamityMod.CalamityWorld.downedDesertScourge;*/

		internal void UpdateCheckboxes()
		{
			checklistList.Clear();

			foreach (BossInfo boss in allBosses)
			{
				if (boss.available())
				{
					if (showCompleted || !boss.downed())
					{
						UIBossCheckbox box = new UIBossCheckbox(boss);
						checklistList.Add(box);
					}
				}
			}

			//if (BossChecklist.instance.thoriumLoaded)
			//{
			//	if (ThoriumModDownedScout)
			//	{
			//		// Add items here
			//	}
			//}
		}


		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			hoverText = "";
			Vector2 MousePosition = new Vector2((float)Main.mouseX, (float)Main.mouseY);
			if (checklistPanel.ContainsPoint(MousePosition))
			{
				Main.player[Main.myPlayer].mouseInterface = true;
			}
		}

		public TextSnippet hoveredTextSnipped;
		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);

			// now we can draw after all other drawing.
			if (hoveredTextSnipped != null)
			{
				hoveredTextSnipped.OnHover();
				if (Main.mouseLeft && Main.mouseLeftRelease)
				{
					hoveredTextSnipped.OnClick();
				}
				hoveredTextSnipped = null;
			}
		}

		public const float SlimeKing = 1f;
		public const float EyeOfCthulhu = 2f;
		public const float EaterOfWorlds = 3f;
		public const float QueenBee = 4f;
		public const float Skeletron = 5f;
		public const float WallOfFlesh = 6f;
		public const float TheTwins = 7f;
		public const float TheDestroyer = 8f;
		public const float SkeletronPrime = 9f;
		public const float Plantera = 10f;
		public const float Golem = 11f;
		public const float DukeFishron = 12f;
		public const float LunaticCultist = 13f;
		public const float Moonlord = 14f;

		List<BossInfo> allBosses = new List<BossInfo> {
			// Bosses -- Vanilla
			new BossInfo("Slime King", SlimeKing, () => true, () => NPC.downedSlimeKing, $"Use [i:{ItemID.SlimeCrown}], randomly in outer 3rds of map, or kill 150 slimes during slime rain."),
			new BossInfo("Eye of Cthulhu", EyeOfCthulhu, () => true, () => NPC.downedBoss1,  $"Use [i:{ItemID.SuspiciousLookingEye}] at night, or 1/3 chance nightly if over 200 HP\nAchievement : [a:EYE_ON_YOU]"),
			new BossInfo("Eater of Worlds / Brain of Cthulhu", EaterOfWorlds, () => true, () => NPC.downedBoss2,  $"Use [i:{ItemID.WormFood}] or [i:{ItemID.BloodySpine}] or break 3 Crimson Hearts or Shadow Orbs"),
			new BossInfo("Queen Bee", QueenBee, () => true, () => NPC.downedQueenBee,  $"Use [i:{ItemID.Abeemination}] or break Larva in Jungle"),
			new BossInfo("Skeletron", Skeletron, () => true, () => NPC.downedBoss3,  $"Visit dungeon or use [i:{ItemID.ClothierVoodooDoll}] at night"),
			new BossInfo("Wall of Flesh", WallOfFlesh, () => true, () => Main.hardMode  ,  $"Spawn by throwing [i:{ItemID.GuideVoodooDoll}] in lava in the Underworld. [c/FF0000:Starts Hardmode!]"),
			new BossInfo("The Twins", TheTwins, () => true, () => NPC.downedMechBoss2,  $"Use [i:{ItemID.MechanicalEye}] at night to spawn"),
			new BossInfo("The Destroyer",TheDestroyer, () => true, () => NPC.downedMechBoss1,  $"Use [i:{ItemID.MechanicalWorm}] at night to spawn"),
			new BossInfo("Skeletron Prime", SkeletronPrime, () => true, () => NPC.downedMechBoss3,  $"Use [i:{ItemID.MechanicalSkull}] at night to spawn"),
			new BossInfo("Plantera", Plantera, () => true, () => NPC.downedPlantBoss,  $"Break a Plantera's Bulb in jungle after 3 Mechanical bosses have been defeated"),
			new BossInfo("Golem", Golem, () => true, () => NPC.downedGolemBoss,  $"Use [i:{ItemID.LihzahrdPowerCell}] on Lihzahrd Altar"),
			new BossInfo("Duke Fishron", DukeFishron, () => true, () => NPC.downedFishron,  $"Fish in ocean using the [i:{ItemID.TruffleWorm}] bait"),
			new BossInfo("Lunatic Cultist", LunaticCultist, () => true, () => NPC.downedAncientCultist,  $"Kill the cultists outside the dungeon post-Golem"),
			new BossInfo("Moonlord", Moonlord, () => true, () => NPC.downedMoonlord,  $"Use [i:{ItemID.CelestialSigil}] or defeat all 4 pillars"),
			// Event Bosses -- Vanilla
			new BossInfo("Nebula Pillar", LunaticCultist + .1f, () => true, () => NPC.downedTowerNebula,  $"Kill the Lunatic Cultist outside the dungeon post-Golem"),
			new BossInfo("Vortex Pillar", LunaticCultist + .2f, () => true, () => NPC.downedTowerVortex,  $"Kill the Lunatic Cultist outside the dungeon post-Golem"),
			new BossInfo("Solar Pillar", LunaticCultist +.3f, () => true, () => NPC.downedTowerSolar,  $"Kill the Lunatic Cultist outside the dungeon post-Golem"),
			new BossInfo("Stardust Pillar", LunaticCultist + .4f, () => true, () => NPC.downedTowerStardust,  $"Kill the Lunatic Cultist outside the dungeon post-Golem"),
			// TODO, all other event bosses...Maybe all pillars as 1?

			// ThoriumMod -- Working, missing some minibosses/bosses?
			new BossInfo("The Grand Thunder Bird", SlimeKing - 0.5f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedThunderBird,  $"Spawn during day by shooting a [i:{ModLoader.GetMod("ThoriumMod")?.ItemType("StormFlare") ?? 0}] with a [i:{ModLoader.GetMod("ThoriumMod")?.ItemType("StrongFlareGun") ?? 0}]"),
			new BossInfo("The Queen Jellyfish", Skeletron - 0.5f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedJelly),
			new BossInfo("Granite Energy Storm", Skeletron + 0.2f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedStorm),
			new BossInfo("The Star Scouter", Skeletron + 0.3f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedScout),
			new BossInfo("The Buried Champion", Skeletron + 0.4f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedChampion),
			new BossInfo("Borean Strider", WallOfFlesh + .05f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedStrider),
			new BossInfo("Coznix, the Fallen Beholder", WallOfFlesh + .1f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedFallenBeholder),
			new BossInfo("The Lich", SkeletronPrime + .1f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedLich),
			new BossInfo("Abyssion, The Forgotten One", Plantera + .1f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedDepthBoss),
			new BossInfo("The Ragnarok", Moonlord + .1f, () => BossChecklist.instance.thoriumLoaded, () => ThoriumMod.ThoriumWorld.downedRealityBreaker),

			// Bluemagic -- Working 100%
			//new BossInfo("Abomination", DukeFishron + 0.2f, () => BossChecklist.instance.bluemagicLoaded, () => Bluemagic.BluemagicWorld.downedAbomination),
			//new BossInfo("Spirit of Purity", Moonlord + 0.9f, () => BossChecklist.instance.bluemagicLoaded, () => Bluemagic.BluemagicWorld.downedPuritySpirit),
			//new BossInfo("Spirit of Chaos", Moonlord + 1.9f, () => BossChecklist.instance.bluemagicLoaded, () => Bluemagic.BluemagicWorld.downedChaosSpirit),

			// Calamity -- Looks like some bosses are still WIP?
			//new BossInfo("Desert Scourge", SlimeKing + .5f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedDesertScourge),
			//new BossInfo("The Hive Mind", QueenBee + .51f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedHiveMind),
			//new BossInfo("The Perforator", QueenBee + .51f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedPerforator),
			//new BossInfo("Slime God", Skeletron + 0.5f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedSlimeGod),
			//new BossInfo("Cryogen", WallOfFlesh + 0.5f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedCryogen),
			//new BossInfo("Calamitas", Plantera - 0.3f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedCalamitas),
			//new BossInfo("Plaguebringer Goliath", Golem + 0.5f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedPlaguebringer),
			//new BossInfo("The Devourer of Gods", Moonlord + 0.5f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedDoG),
			//new BossInfo("Jungle Dragon, Yharon", Moonlord + 1.5f, () => BossChecklist.instance.calamityLoaded, () => CalamityMod.CalamityWorld.downedYharon),
			// CalamityMod.CalamityWorld.downedYharon
			
			// SacredTools -- Working 100%
			//new BossInfo("Grand Harpy", Skeletron + .3f, () => BossChecklist.instance.sacredToolsLoaded, () => SacredTools.ModdedWorld.downedHarpy),
			//new BossInfo("Harpy Queen, Raynare", Plantera - 0.4f, () => BossChecklist.instance.sacredToolsLoaded, () => SacredTools.ModdedWorld.downedRaynare),
			//new BossInfo("Abaddon", LunaticCultist + .5f, () => BossChecklist.instance.sacredToolsLoaded, () => SacredTools.ModdedWorld.downedAbaddon),
			//new BossInfo("Flare Serpent", Moonlord + .2f, () => BossChecklist.instance.sacredToolsLoaded, () => SacredTools.ModdedWorld.FlariumSpawns),
			//new BossInfo("Lunarians", Moonlord + .3f, () => BossChecklist.instance.sacredToolsLoaded, () => SacredTools.ModdedWorld.downedLunarians),

			// Joost
			//new BossInfo("Jumbo Cactuar", Moonlord + 0.7f, () => BossChecklist.instance.joostLoaded, () => JoostMod.JoostWorld.downedJumboCactuar),
			//new BossInfo("SA-X", Moonlord + 0.8f, () => BossChecklist.instance.joostLoaded, () => JoostMod.JoostWorld.downedSAX),

			// CrystiliumMod -- Need exposed downedBoss bools
			//new BossInfo(, 0.1f, () => BossChecklist.instance.crystiliumLoaded, () => CrystiliumMod.CrystalWorld.),
			
			// Pumpking -- downedBoss bools incorrectly programed
			//new BossInfo("Pumpking Horseman", DukeFishron + 0.3f, () => BossChecklist.instance.pumpkingLoaded, () => Pumpking.PumpkingWorld.downedPumpkingHorseman),
			//new BossInfo("Terra Lord", Moonlord + 0.4f, () => BossChecklist.instance.pumpkingLoaded, () => Pumpking.PumpkingWorld.downedTerraLord),
		};

		internal void AddBoss(string bossname, float bossValue, Func<bool> bossDowned, string bossInfo = null)
		{
			allBosses.Add(new BossInfo(bossname, bossValue, () => true, bossDowned, bossInfo));
		}

		//			Calamity
		//	--King Slime
		//	Desert Scourge
		//	Sea Dragon Leviathan (WIP) - Meant to be fought in Hardmode but can be fought earlier for a challenge
		//	--Eye of Cthulhu
		//	--Eater of Worlds/Brain of Cthulhu
		//	--Queen Bee
		//	The Perforator/The Hive Mind
		//	--Skeletron
		//	Slime God
		//	--Wall of Flesh
		//	Cryogen
		//	--The Destroyer/Skeletron Prime/The Twins
		//	Calamitas
		//	--Plantera
		//	The Devourer of Gods
		//	--The Golem
		//	Plaguebringer Goliath
		//	--Duke Fishron
		//	--Lunatic Cultist
		//	--Moon Lord
		//	Providence, the Profaned God (Currently being programmed and sprited, Hallowed Boss) Profaned Guardian (Miniboss that Providence spawns every 10% life)
		//	Alpha & Omega (WIP)
		//	Andromeda (Martian Star Destroyer) (WIP, Space Boss)
		//	Dargon
		//	Yharim, Lord of the Cosmos/Soul of Yharim (WIP)

		//			Thorium
		//The Grand Thunder Bird -- 1000 hp: Grand Flare Gun, Storm flare to summon
		//The Queen Jellyfish - Pre Skeletron boss / 4000 -- Jellyfish Resonator
		//Granite Energy Storm -- 4250 hp, Unstable Core, post skele
		//The Star Scouter - Post Skeletron Sky boss / 10000	-- Star Caller
		//Coznix, the Fallen Beholder - Early Hardmode Underworld boss / 14000 Life -- Void Lens
		//The Lich - Post mechanical Hardmode Boss / 16500 -- Grim Harvest Sigil 
		//The Ragnarok - Doom Sayer's Coin - 1 of each pillar fragment

		//			BlueMagic:
		//Abomination -- Post fishron - Foul Orb
		//Spirit of Purity --  Elemental Purge

		//			SacredTools:
		//	?????Abaddon -- 80000 -- Key of Obliteration
		//	Grand Harpy -- after skeltron Feather Talisman  -- fether and sunplate blocks
		//	Grand Harpy Queen -- post mechs, after skeletron prime -  Golden Feather Talisman
		//	oblivion shade - post cultist
		//	The Flare Serpent -- post moon, before spirit of purity - 300000HP --Obsidian Core

		//		Crystilium
		//	Crystal King -- Around Duke: summon at fountain with cryptic crystal

	}

	public class BossInfo
	{
		internal Func<bool> available;
		internal Func<bool> downed;
		internal string name;
		internal float progression;
		//internal int spawnItemID;
		internal string info;

		public BossInfo(string name, float progression, Func<bool> available, Func<bool> downed, string info = null)
		{
			this.name = name;
			this.progression = progression;
			this.available = available;
			this.downed = downed;
			this.info = info;
		}
	}

	public class FixedUIScrollbar : UIScrollbar
	{
		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			UserInterface temp = UserInterface.ActiveInstance;
			UserInterface.ActiveInstance = BossChecklist.bossChecklistInterface;
			base.DrawSelf(spriteBatch);
			UserInterface.ActiveInstance = temp;
		}

		public override void MouseDown(UIMouseEvent evt)
		{
			UserInterface temp = UserInterface.ActiveInstance;
			UserInterface.ActiveInstance = BossChecklist.bossChecklistInterface;
			base.MouseDown(evt);
			UserInterface.ActiveInstance = temp;
		}
	}
}

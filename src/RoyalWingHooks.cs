using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RoyalHunter
{
    internal static class RoyalWingHooks
    {
        /// <summary>ID персонажа в slugbase/royalhunter.json.</summary>
        public const string SlugcatId = "RoyalHunter";

        private static readonly ConditionalWeakTable<Player, RoyalWings> Table = new ConditionalWeakTable<Player, RoyalWings>();

        public static bool IsRoyal(Player player)
        {
            return player != null && player.SlugCatClass != null && player.SlugCatClass.value == SlugcatId;
        }

        private static bool TryGet(Player player, out RoyalWings wings)
        {
            wings = null;
            return player != null && Table.TryGetValue(player, out wings) && wings != null;
        }

        private static RoyalWings GetOrCreate(Player player)
        {
            if (!IsRoyal(player))
                return null;
            if (!Table.TryGetValue(player, out var wings) || wings == null)
            {
                wings = new RoyalWings(player);
                Table.Remove(player);
                Table.Add(player, wings);
            }
            return wings;
        }

        public static void Apply()
        {
            On.Player.ctor += Player_ctor;
            On.Player.Update += Player_Update;
            On.Player.MovementUpdate += Player_MovementUpdate;
            On.Player.Jump += Player_Jump;
            On.Player.NewRoom += Player_NewRoom;

            On.PlayerGraphics.ctor += PlayerGraphics_ctor;
            On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;
            On.PlayerGraphics.Update += PlayerGraphics_Update;
            On.PlayerGraphics.Reset += PlayerGraphics_Reset;
        }

        #region Player
        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            try { GetOrCreate(self); }
            catch (Exception e) { RoyalHunterPlugin.Report("Player.ctor", e); }
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);
            if (TryGet(self, out var wings))
            {
                try { wings.Update(); }
                catch (Exception e) { RoyalHunterPlugin.Report("Player.Update", e); }
            }
        }

        private static void Player_MovementUpdate(On.Player.orig_MovementUpdate orig, Player self, bool eu)
        {
            RoyalWings wings;
            bool flying = TryGet(self, out wings) && wings.IsFlying && self.Consious;
            if (flying)
            {
                self.bodyMode = Player.BodyModeIndex.Default;
                self.animation = Player.AnimationIndex.None;
            }

            orig(self, eu);

            if (flying)
            {
                try { wings.AfterMovementUpdate(self); }
                catch (Exception e) { RoyalHunterPlugin.Report("Player.MovementUpdate", e); }
            }
        }

        private static void Player_Jump(On.Player.orig_Jump orig, Player self)
        {
            orig(self);
            if (TryGet(self, out var wings))
            {
                try { wings.Jump(); }
                catch (Exception e) { RoyalHunterPlugin.Report("Player.Jump", e); }
            }
        }

        private static void Player_NewRoom(On.Player.orig_NewRoom orig, Player self, Room newRoom)
        {
            orig(self, newRoom);
            if (TryGet(self, out var wings))
            {
                try { wings.NewRoom(self.room); }
                catch (Exception e) { RoyalHunterPlugin.Report("Player.NewRoom", e); }
            }
        }
        #endregion

        #region PlayerGraphics
        private static void PlayerGraphics_ctor(On.PlayerGraphics.orig_ctor orig, PlayerGraphics self, PhysicalObject ow)
        {
            orig(self, ow);
            try
            {
                RoyalWings wings = GetOrCreate(self.player);
                if (wings != null)
                    wings.Build(self);
            }
            catch (Exception e) { RoyalHunterPlugin.Report("PlayerGraphics.ctor", e); }
        }

        private static void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);
            if (TryGet(self.player, out var wings))
            {
                try { wings.InitiateSprites(self, sLeaser, rCam); }
                catch (Exception e) { RoyalHunterPlugin.Report("PlayerGraphics.InitiateSprites", e); }
            }
        }

        private static void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            orig(self, sLeaser, rCam, newContatiner);
            if (TryGet(self.player, out var wings))
            {
                try { wings.AddToContainer(self, sLeaser, rCam, newContatiner); }
                catch (Exception e) { RoyalHunterPlugin.Report("PlayerGraphics.AddToContainer", e); }
            }
        }

        private static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (TryGet(self.player, out var wings))
            {
                try { wings.DrawSprites(self, sLeaser, rCam, timeStacker, camPos); }
                catch (Exception e) { RoyalHunterPlugin.Report("PlayerGraphics.DrawSprites", e); }
            }
        }

        private static void PlayerGraphics_ApplyPalette(On.PlayerGraphics.orig_ApplyPalette orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig(self, sLeaser, rCam, palette);
            if (TryGet(self.player, out var wings))
            {
                try { wings.ApplyPalette(self, sLeaser, rCam, palette); }
                catch (Exception e) { RoyalHunterPlugin.Report("PlayerGraphics.ApplyPalette", e); }
            }
        }

        private static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            orig(self);
            if (TryGet(self.player, out var wings))
            {
                try { wings.GraphicsUpdate(self); }
                catch (Exception e) { RoyalHunterPlugin.Report("PlayerGraphics.Update", e); }
            }
        }

        private static void PlayerGraphics_Reset(On.PlayerGraphics.orig_Reset orig, PlayerGraphics self)
        {
            orig(self);
            if (TryGet(self.player, out var wings))
            {
                try { wings.Reset(self); }
                catch (Exception e) { RoyalHunterPlugin.Report("PlayerGraphics.Reset", e); }
            }
        }
        #endregion
    }
}

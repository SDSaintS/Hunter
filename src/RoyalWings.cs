using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RoyalHunter
{
    /// <summary>Волна цвета, бегущая по перьям (аналог VultureGraphics.WingColorWave).</summary>
    internal class WingColorWave
    {
        public int delay;
        public float position;
        public float lastPosition;
        public float speed;
        public float saturation;
        public float lightness;
        public float forceAlpha;

        public WingColorWave(int delay, float speed, float saturation, float lightness, float forceAlpha)
        {
            this.delay = delay;
            this.speed = speed;
            this.saturation = saturation;
            this.lightness = lightness;
            this.forceAlpha = forceAlpha;
        }
    }

    /// <summary>
    /// Крылья королевского стервятника для игрока: графика (щупальца + перья) и полёт.
    /// Перенесено из VultureCat (Random Buff) без бивней, маски и системы баффов.
    /// </summary>
    internal class RoyalWings
    {
        private readonly WeakReference<Player> ownerRef;

        // Эти свойства читают скопированные классы крыльев.
        public bool IsKing => true;
        public bool IsMiros => false;
        public bool albino => false;

        public HSLColor ColorA;
        public HSLColor ColorB;

        #region Параметры крыльев
        public float wingLength;
        public float wingWidth;
        private float featherLength;
        private float featherWidth;
        public float groundRetractionScale = 0.9f;

        public RoyalWingTentacle[,] wings;
        public float[,] wingHealth;
        private RoyalWingFeather[,,] feather;
        private List<WingColorWave> colorWaves = new List<WingColorWave>();
        private int feathersPerWing;
        #endregion

        #region Полёт
        public bool isFlying;
        public bool hangingInTentacle;
        public bool hoverStill;
        public Vector2 moveDirection;
        public Vector2 wantPos;
        public float wingFlap;
        public float wingFlapAmplitude = 1f;

        private bool wantPosIsSetX;
        private bool wantPosIsSetY;
        private int preventFlight;
        private bool exhausted;
        private bool physicsModified;
        private float wingSpeed;

        private const float NormalGravity = 0.9f;
        private const float NormalAirFriction = 0.999f;
        private const float FlightAirFriction = 0.7f;
        private const float DefaultWingSpeed = 10f;
        // Порог массы, после которого полёт утомляет (как у StackLayer = 3 в оригинале: 1 + 0.1 * 3).
        private const float MassFacThreshold = 1.3f;
        #endregion

        #region Номера спрайтов
        private int firstSprite = -1;

        private int WingsSpriteStart => firstSprite;
        private int FeatherSpriteStart => WingsSpriteStart + wings.Length;
        private int FeatherSpritesLength => feathersPerWing * 2 * wings.Length;
        private int TotalSprites => wings.Length + FeatherSpritesLength;

        private int WingsSprite(int index, int side)
        {
            return WingsSpriteStart + index * wings.GetLength(1) + side;
        }

        private int FeatherSprite(int index, int side, int i)
        {
            return FeatherSpriteStart + i * 2 + feathersPerWing * (index * wings.GetLength(1) + side) * 2;
        }

        private int FeatherColorSprite(int index, int side, int i)
        {
            return FeatherSpriteStart + i * 2 + 1 + feathersPerWing * (index * wings.GetLength(1) + side) * 2;
        }
        #endregion

        public bool Built => wings != null && feather != null;

        public RoyalWings(Player player)
        {
            ownerRef = new WeakReference<Player>(player);
            wingSpeed = DefaultWingSpeed;

            if (player.playerState != null && player.playerState.isPup)
            {
                wingLength = 3f;
                wingWidth = 0.3f;
                featherLength = 1.2f;
                featherWidth = 0.3f;
            }
            else
            {
                wingLength = 5f;
                wingWidth = 0.5f;
                featherLength = 1.8f;
                featherWidth = 0.5f;
            }

            // Королевская палитра: багровая основа, золотая кайма.
            ColorB = new HSLColor(0.99f, 0.9f, 0.5f);
            ColorA = new HSLColor(0.12f, 0.6f, 0.72f);
        }

        public bool isLaserActive()
        {
            return false;
        }

        #region Создание
        public void Build(PlayerGraphics self)
        {
            Player player = self.owner as Player;
            if (player == null)
                return;

            wingHealth = new float[1, 2];
            for (int j = 0; j < 2; j++)
                wingHealth[0, j] = 1f;

            wings = new RoyalWingTentacle[1, 2];
            for (int j = 0; j < wings.GetLength(1); j++)
                wings[0, j] = new RoyalWingTentacle(player, player.bodyChunks[0], 9f * wingLength * 4f, 0, j, this);

            feathersPerWing = Random.Range(15, 25);
            colorWaves = new List<WingColorWave>();
            feather = new RoyalWingFeather[1, 2, feathersPerWing];
            for (int j = 0; j < wings.GetLength(1); j++)
            {
                for (int k = 0; k < feathersPerWing; k++)
                {
                    float pos = ((float)k + 0.5f) / (float)feathersPerWing;
                    float wingPos = Mathf.Lerp(1f - Mathf.Pow(0.89f, (float)k), Mathf.Sqrt(pos), 0.5f);
                    wingPos = Mathf.InverseLerp(0.1f, 1.1f, wingPos);
                    feather[0, j, k] = new RoyalWingFeather(
                        self,
                        wings[0, j],
                        wingPos,
                        featherLength * RoyalWingTentacle.FeatherContour(pos, 0f) * Mathf.Lerp(10f, 15f, Random.value),
                        featherLength * RoyalWingTentacle.FeatherContour(pos, 1f) * Mathf.Lerp(13f, 15f, Random.value) * 1.3f,
                        featherWidth * Mathf.Lerp(3f, 6f, RoyalWingTentacle.FeatherWidth(pos)),
                        this);
                }
            }

            Reset(self);
        }

        public void Reset(PlayerGraphics self)
        {
            if (!Built)
                return;
            Vector2 pos = self.owner.bodyChunks[0].pos;
            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    wings[i, j].Reset(pos);
            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    for (int k = 0; k < feathersPerWing; k++)
                        feather[i, j, k].Reset(pos);
        }
        #endregion

        #region Графика
        private bool SpritesValid(RoomCamera.SpriteLeaser sLeaser)
        {
            if (!Built || sLeaser == null || sLeaser.sprites == null || firstSprite < 1)
                return false;
            if (sLeaser.sprites.Length < firstSprite + TotalSprites)
                return false;
            for (int k = 0; k < TotalSprites; k++)
                if (sLeaser.sprites[firstSprite + k] == null)
                    return false;
            return true;
        }

        public void InitiateSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            if (!Built)
                return;

            // Спрайты крыльев добавляются в конец массива спрайтов игрока.
            firstSprite = sLeaser.sprites.Length;
            Array.Resize(ref sLeaser.sprites, firstSprite + TotalSprites);

            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    sLeaser.sprites[WingsSprite(i, j)] = TriangleMesh.MakeLongMesh(wings[i, j].tChunks.Length, false, false);

            for (int i = 0; i < wings.GetLength(0); i++)
            {
                for (int j = 0; j < wings.GetLength(1); j++)
                {
                    for (int l = 0; l < feathersPerWing; l++)
                    {
                        sLeaser.sprites[FeatherSprite(i, j, l)] = new FSprite("KrakenFeather", true);
                        sLeaser.sprites[FeatherSprite(i, j, l)].anchorY = 0.97f;
                        sLeaser.sprites[FeatherColorSprite(i, j, l)] = new FSprite("KrakenFeatherColor", true);
                        sLeaser.sprites[FeatherColorSprite(i, j, l)].anchorY = 0.97f;
                    }
                }
            }

            self.AddToContainer(sLeaser, rCam, null);
        }

        public void DrawSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (!SpritesValid(sLeaser))
                return;

            for (int k = 0; k < wings.GetLength(0); k++)
            {
                for (int l = 0; l < wings.GetLength(1); l++)
                {
                    RoyalWingTentacle wing = wings[k, l];
                    TriangleMesh mesh = sLeaser.sprites[WingsSprite(k, l)] as TriangleMesh;

                    Vector2 prev = Vector2.Lerp(wing.connectedChunk.lastPos, wing.connectedChunk.pos, timeStacker);
                    for (int m = 0; m < wing.tChunks.Length; m++)
                    {
                        Vector2 cur = Vector2.Lerp(wing.tChunks[m].lastPos, wing.tChunks[m].pos, timeStacker);
                        Vector2 dir = (cur - prev).normalized;
                        Vector2 perp = Custom.PerpendicularVector(dir);
                        float d = Vector2.Distance(cur, prev) / 5f;
                        float w = wing.TentacleContour(((float)m + 0.5f) / (float)wing.tChunks.Length);
                        w *= Mathf.Clamp(Mathf.Pow(wing.tChunks[m].stretchedFac, 0.35f), 0.5f, 1.5f);
                        mesh.MoveVertice(m * 4, prev - perp * w + dir * d - camPos);
                        mesh.MoveVertice(m * 4 + 1, prev + perp * w + dir * d - camPos);
                        w = wing.TentacleContour(((float)m + 1f) / (float)wing.tChunks.Length);
                        w *= Mathf.Clamp(Mathf.Pow(wing.tChunks[m].stretchedFac, 0.35f), 0.5f, 1.5f);
                        mesh.MoveVertice(m * 4 + 2, cur - perp * w - dir * d - camPos);
                        mesh.MoveVertice(m * 4 + 3, cur + perp * w - dir * d - camPos);
                        prev = cur;
                    }

                    for (int n = 0; n < feathersPerWing; n++)
                    {
                        RoyalWingFeather f = feather[k, l, n];
                        FSprite body = sLeaser.sprites[FeatherSprite(k, l, n)];
                        FSprite color = sLeaser.sprites[FeatherColorSprite(k, l, n)];

                        Vector2 root = Vector2.Lerp(f.ConnectedLastPos, f.ConnectedPos, timeStacker);
                        Vector2 tip = Vector2.Lerp(f.lastPos, f.pos, timeStacker);
                        float rotation = Custom.AimFromOneVectorToAnother(tip, root);
                        float length = Vector2.Distance(root, tip) / 107f;
                        float scaleX = Mathf.Lerp(3f, f.width, (f.extendedFac + wing.flyingMode) * 0.5f) / 9f * ((l == 0) ? 1f : -1f) * 1.3f;

                        body.x = root.x - camPos.x;
                        body.y = root.y - camPos.y;
                        body.rotation = rotation;
                        body.scaleX = scaleX;
                        body.scaleY = length;

                        color.x = root.x - camPos.x;
                        color.y = root.y - camPos.y;
                        color.rotation = rotation;
                        color.scaleX = scaleX;
                        color.scaleY = length;
                        color.color = f.CurrentColor();
                    }
                }
            }
        }

        public void ApplyPalette(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            if (!SpritesValid(sLeaser))
                return;
            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    sLeaser.sprites[WingsSprite(i, j)].color = palette.blackColor;
        }

        public void AddToContainer(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            if (!SpritesValid(sLeaser))
                return;
            if (newContatiner == null)
                newContatiner = rCam.ReturnFContainer("Midground");

            for (int k = 0; k < TotalSprites; k++)
            {
                FSprite sprite = sLeaser.sprites[firstSprite + k];
                sprite.RemoveFromContainer();
                newContatiner.AddChild(sprite);
                // Крылья рисуются позади тела.
                sprite.MoveBehindOtherNode(sLeaser.sprites[0]);
            }
        }

        public void GraphicsUpdate(PlayerGraphics self)
        {
            if (!Built)
                return;

            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    for (int k = 0; k < feathersPerWing; k++)
                        feather[i, j, k].Update();

            if (Random.value < 0.005f)
                MakeColorWave(0);

            for (int n = colorWaves.Count - 1; n >= 0; n--)
            {
                if (colorWaves[n].lastPosition > 1f)
                {
                    colorWaves.RemoveAt(n);
                }
                else if (colorWaves[n].delay > 0)
                {
                    colorWaves[n].delay--;
                }
                else
                {
                    colorWaves[n].lastPosition = colorWaves[n].position;
                    colorWaves[n].position += colorWaves[n].speed / (1f + colorWaves[n].position);
                    for (int i = 0; i < wings.GetLength(0); i++)
                    {
                        for (int j = 0; j < wings.GetLength(1); j++)
                        {
                            for (int k = 0; k < feathersPerWing; k++)
                            {
                                RoyalWingFeather f = feather[i, j, k];
                                if (colorWaves[n].lastPosition < f.wingPosition && colorWaves[n].position >= f.wingPosition)
                                {
                                    f.saturationBonus = Mathf.Max(f.saturationBonus, colorWaves[n].saturation);
                                    f.lightnessBonus = Mathf.Max(f.lightnessBonus, colorWaves[n].lightness);
                                    f.forcedAlpha = Mathf.Max(f.forcedAlpha, colorWaves[n].forceAlpha);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void MakeColorWave(int delay)
        {
            colorWaves.Add(new WingColorWave(
                delay,
                1f / Mathf.Lerp(10f, 30f, Random.value),
                1f - Random.value * Random.value * Random.value,
                1f - Random.value * Random.value * Random.value,
                1f - Random.value * Random.value * Random.value));
        }
        #endregion

        #region Поведение
        private float MassFac
        {
            get
            {
                if (!ownerRef.TryGetTarget(out var player))
                    return 1f;

                float graspMass = 0f;
                if (player.grasps != null)
                {
                    for (int i = 0; i < player.grasps.Length; i++)
                    {
                        if (player.grasps[i] != null)
                            graspMass += player.grasps[i].grabbed.TotalMass;
                    }
                }
                return (player.TotalMass + graspMass) / player.slugcatStats.runspeedFac;
            }
        }

        private bool MassFacCondition => MassFac >= MassFacThreshold;

        public bool IsFlying => isFlying;

        public void StopFlight()
        {
            isFlying = false;
            if (wings != null)
            {
                for (int i = 0; i < wings.GetLength(0); i++)
                    for (int j = 0; j < wings.GetLength(1); j++)
                        wings[i, j].SwitchMode(RoyalWingTentacle.Mode.Climb);
            }
        }

        public void InitiateFlight(Player self)
        {
            if (wings == null || self.input[0].y < 0)
                return;
            self.bodyMode = Player.BodyModeIndex.Default;
            self.animation = Player.AnimationIndex.None;
            self.wantToJump = 0;
            isFlying = true;
            wantPos = self.bodyChunks[0].pos;

            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    wings[i, j].SwitchMode(RoyalWingTentacle.Mode.Fly);
        }

        public bool CanSustainFlight(Player self)
        {
            return preventFlight == 0
                && !exhausted
                && self.canJump <= 0
                && self.Consious && !self.Stunned
                && self.bodyMode != Player.BodyModeIndex.Crawl
                && self.bodyMode != Player.BodyModeIndex.CorridorClimb
                && self.bodyMode != Player.BodyModeIndex.ClimbIntoShortCut
                && self.bodyMode != Player.BodyModeIndex.WallClimb
                && self.bodyMode != Player.BodyModeIndex.Swimming
                && self.animation != Player.AnimationIndex.HangFromBeam
                && self.animation != Player.AnimationIndex.ClimbOnBeam
                && self.animation != Player.AnimationIndex.AntlerClimb
                && self.animation != Player.AnimationIndex.VineGrab
                && self.animation != Player.AnimationIndex.ZeroGPoleGrab
                && self.animation != Player.AnimationIndex.HangUnderVerticalBeam;
        }

        public bool CheckTentacleModeOr(RoyalWingTentacle.Mode mode)
        {
            bool flag = false;
            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    flag = flag || wings[i, j].mode == mode;
            return flag;
        }

        public bool CheckTentacleModeAnd(RoyalWingTentacle.Mode mode)
        {
            bool flag = true;
            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    flag = flag && wings[i, j].mode == mode;
            return flag;
        }

        /// <summary>Вызывается из Player.Update (после оригинала).</summary>
        public void Update()
        {
            if (!ownerRef.TryGetTarget(out var player) || !Built)
                return;

            if (!player.Consious)
                StopFlight();

            hangingInTentacle = false;
            if (player.room != null)
            {
                for (int i = 0; i < wings.GetLength(0); i++)
                    for (int j = 0; j < wings.GetLength(1); j++)
                        wings[i, j].Update();
            }

            if (player.animation == Player.AnimationIndex.HangFromBeam || player.animation == Player.AnimationIndex.SurfaceSwim)
                preventFlight = 15;
            else if (player.bodyMode == Player.BodyModeIndex.WallClimb)
                preventFlight = 10;
            else if (preventFlight > 0)
                preventFlight--;

            if (exhausted && !isFlying && player.aerobicLevel < 0.5f)
                exhausted = false;

            moveDirection = new Vector2(player.input[0].x, player.input[0].y).normalized;

            if (player.wantToJump > 0)
            {
                if (isFlying)
                {
                    StopFlight();
                    preventFlight = 10;
                }
                else if (CanSustainFlight(player))
                {
                    InitiateFlight(player);
                }
            }

            if (isFlying)
            {
                player.AerobicIncrease(0.04f * Mathf.Pow(MassFac, 2f));
                if (MassFacCondition)
                {
                    if (player.aerobicLevel >= 0.5f)
                        wingFlapAmplitude = Mathf.Clamp(wingFlapAmplitude - 0.05f, 1f - player.aerobicLevel, 1f);
                    if (player.aerobicLevel >= 0.95f)
                        exhausted = true;
                }
                if (exhausted)
                {
                    for (int i = 0; i < wings.GetLength(0); i++)
                        for (int j = 0; j < wings.GetLength(1); j++)
                            wings[i, j].SwitchMode(RoyalWingTentacle.Mode.Climb);
                }

                physicsModified = true;
                player.gravity = 0f;
                player.airFriction = FlightAirFriction;

                FlightUpdate(player);

                if (CheckTentacleModeAnd(RoyalWingTentacle.Mode.Climb))
                    StopFlight();
            }
            else
            {
                // Возвращаем физику один раз после посадки, а не перезаписываем её каждый кадр.
                if (physicsModified)
                {
                    physicsModified = false;
                    player.gravity = NormalGravity;
                    player.airFriction = NormalAirFriction;
                }
                wantPos = player.bodyChunks[0].pos + wingSpeed * new Vector2(player.input[0].x, player.input[0].y);
            }
        }

        private void FlightUpdate(Player player)
        {
            if (player.room == null)
                return;

            float massSpeedFac = 1f;
            if (player.aerobicLevel >= 0.5f && MassFacCondition)
            {
                massSpeedFac = Custom.LerpMap(player.aerobicLevel, 0.5f, 1f, 1f, 0f);
                wingSpeed = Custom.LerpMap(player.aerobicLevel, 0.5f, 1f, DefaultWingSpeed, 0f);
                wantPos += Custom.LerpMap(player.aerobicLevel, 0.5f, 1f, 0f, 4f) * Vector2.down;
            }
            else
            {
                wingSpeed = DefaultWingSpeed;
            }

            wantPos += wingSpeed * new Vector2(player.input[0].x, player.input[0].y);
            if (player.input[0].x == 0 && !wantPosIsSetX)
            {
                player.bodyChunks[0].vel.x *= 0.8f;
                player.bodyChunks[1].vel.x *= 0.8f;
                if (Mathf.Abs(player.bodyChunks[0].vel.x) < 1f)
                {
                    wantPosIsSetX = true;
                    wantPos.x = player.bodyChunks[0].pos.x;
                }
            }
            if (player.input[0].y == 0 && !wantPosIsSetY)
            {
                player.bodyChunks[0].vel.y *= 0.8f;
                player.bodyChunks[1].vel.y *= 0.8f;
                if (Mathf.Abs(player.bodyChunks[0].vel.y) < 1f)
                {
                    wantPosIsSetY = true;
                    wantPos.y = player.bodyChunks[0].pos.y;
                }
            }
            if (player.input[0].x != 0 || Mathf.Abs(wantPos.x - player.bodyChunks[0].pos.x) > 100f)
                wantPosIsSetX = false;
            if (player.input[0].y != 0 || Mathf.Abs(wantPos.y - player.bodyChunks[0].pos.y) > 100f)
                wantPosIsSetY = false;

            player.bodyChunks[0].vel *= Custom.LerpMap(player.bodyChunks[0].vel.magnitude, 1f, 6f, 0.99f, 0.9f);
            player.bodyChunks[0].vel += Vector2.ClampMagnitude(wantPos - player.bodyChunks[0].pos, 100f) / 100f * 3f;

            // Ускорение при наклоне вперёд.
            if (player.input[0].x != 0 && player.input[0].y < 0)
                player.bodyChunks[0].vel.x *= 1.2f;

            // Компенсация гравитации.
            if (player.input[0].y >= 0)
                player.bodyChunks[0].vel += 1.85f * Vector2.up * player.room.gravity * massSpeedFac;
            else
                player.bodyChunks[0].vel -= 1.05f * Vector2.up * massSpeedFac;

            player.bodyChunks[0].vel += Custom.RNV() * Random.value * 0.5f;
            player.bodyChunks[1].vel *= 0.8f;

            // Взмахи крыльев.
            if (wingFlap < 0.5f)
                wingFlap += 0.033333335f;
            else
                wingFlap += 0.02f;
            if (wingFlap > 1f)
                wingFlap -= 1f;
            if (player.input[0].y < 0)
                wingFlap = 0f;

            if (CheckTentacleModeOr(RoyalWingTentacle.Mode.Fly))
                wingFlapAmplitude = Mathf.Clamp(wingFlapAmplitude + 0.033333335f, 0f, 1f);
            else if (CheckTentacleModeAnd(RoyalWingTentacle.Mode.Climb))
                wingFlapAmplitude = 0f;
            else
                wingFlapAmplitude = Mathf.Clamp(wingFlapAmplitude + 0.0125f, 0f, 0.5f);

            hoverStill = false;
        }

        /// <summary>Вызывается из Player.MovementUpdate после оригинала, пока крылья в полёте.</summary>
        public void AfterMovementUpdate(Player player)
        {
            if (!isFlying)
                return;

            if (!CanSustainFlight(player))
            {
                StopFlight();
                return;
            }

            player.bodyMode = Player.BodyModeIndex.Default;
            player.animation = player.input[0].x != 0 ? Player.AnimationIndex.LedgeCrawl : Player.AnimationIndex.None;
        }

        public void Jump()
        {
            if (wings == null)
                return;
            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    if (wings[i, j].hasAnyGrip)
                        wings[i, j].ReleaseGrip();
        }

        public void NewRoom(Room room)
        {
            if (wings == null)
                return;
            for (int i = 0; i < wings.GetLength(0); i++)
                for (int j = 0; j < wings.GetLength(1); j++)
                    wings[i, j].NewRoom(room);
        }
        #endregion
    }
}

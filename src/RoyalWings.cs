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
                        sLeaser


using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace RoyalHunter
{
    internal class RoyalWingFeather : BodyPart
    {
        private enum ContractMode
        {
            Even,
            Jerky,
            Jammed
        }

        public PlayerGraphics kGraphics;
        public RoyalWingTentacle wing;
        private RoyalWings vultureCat;
        public float wingPosition;
        private float ef;
        public float width;
        public float contractedLength;
        public float extendedLength;
        private ContractMode contractMode;
        public float contractSpeed;
        public float lose;
        public float brokenColor;
        public float forcedAlpha;
        public float lightnessBonus;
        public float saturationBonus;
        private int terrainContactTimer;

        public float extendedFac
        {
            get
            {
                return ef;
            }
            set
            {
                ef = Mathf.Clamp(value, 0f, 1f);
            }
        }

        public float CurrentLength => Mathf.Lerp(contractedLength, extendedLength, extendedFac);

        public Tentacle.TentacleChunk PreviousPreviousChunk => wing.tChunks[Custom.IntClamp(Mathf.FloorToInt(wingPosition * (float)wing.tChunks.Length) - 1, 0, wing.tChunks.Length - 1)];

        public Tentacle.TentacleChunk PreviousChunk => wing.tChunks[Custom.IntClamp(Mathf.FloorToInt(wingPosition * (float)wing.tChunks.Length), 0, wing.tChunks.Length - 1)];

        public Tentacle.TentacleChunk NextChunk => wing.tChunks[Custom.IntClamp(Mathf.FloorToInt(wingPosition * (float)wing.tChunks.Length) + 1, 0, wing.tChunks.Length - 1)];

        public float BetweenChunksLerp => wingPosition * (float)wing.tChunks.Length - Mathf.Floor(wingPosition * (float)wing.tChunks.Length);

        public Vector2 ConnectedPos => Vector2.Lerp(PreviousChunk.pos, NextChunk.pos, BetweenChunksLerp);

        public Vector2 ConnectedLastPos => Vector2.Lerp(PreviousChunk.lastPos, NextChunk.lastPos, BetweenChunksLerp);

        public RoyalWingFeather(PlayerGraphics kGraphics, RoyalWingTentacle wing, float wingPosition, float contractedLength, float extendedLength, float width, RoyalWings vultureCat)
            : base(kGraphics)
        {
            this.kGraphics = kGraphics;
            this.wing = wing;
            this.wingPosition = wingPosition;
            this.contractedLength = contractedLength;
            this.extendedLength = extendedLength;
            this.width = width;
            this.vultureCat = vultureCat;
            lose = 0f;
        }

        public override void Update()
        {
            base.Update();
            if (kGraphics.owner.room.PointSubmerged(pos))
            {
                vel *= 0.1f;
            }
            lastPos = pos;
            pos += vel;
            vel *= 0.7f;
            Vector2 normalized = Vector2.Lerp(PreviousChunk.pos - PreviousPreviousChunk.pos, NextChunk.pos - PreviousChunk.pos, (PreviousPreviousChunk == PreviousChunk) ? 1f : BetweenChunksLerp).normalized;
            Vector2 vector = Custom.PerpendicularVector(normalized) * (vultureCat.IsMiros ? GetTentacleAngle(wing.index, wing.side) : ((wing.side == 1) ? (-1f) : 1f));
            float num = Mathf.Lerp(Mathf.Lerp(1f, Mathf.Lerp(-0.9f, 1.5f, Mathf.InverseLerp(wing.idealLength * 0.5f, wing.idealLength, Vector2.Distance(wing.FloatBase, wing.Tip.pos))), wingPosition), Mathf.Lerp(-0.5f, 4f, wingPosition), extendedFac);
            Vector2 vector2 = ConnectedPos + (vector + normalized * num).normalized * CurrentLength;
            vel += (vector2 - pos) * Mathf.Lerp(0.3f, 0.8f, wing.flyingMode) * (1f - lose);
            if (wing.flyingMode > extendedFac)
            {
                extendedFac += 1f / Mathf.Lerp(10f, 40f, UnityEngine.Random.value);
            }
            else if (wing.flyingMode < extendedFac)
            {
                if (extendedFac == 1f)
                {
                    contractMode = (vultureCat.IsMiros ? ContractMode.Jerky : ContractMode.Even);
                    contractSpeed = 1f / Mathf.Lerp(20f, 800f, UnityEngine.Random.value * UnityEngine.Random.value * UnityEngine.Random.value);
                    if (UnityEngine.Random.value < 0.3f)
                    {
                        if (UnityEngine.Random.value < 0.7f)
                        {
                            contractMode = ContractMode.Jerky;
                        }
                        else
                        {
                            contractMode = ContractMode.Jammed;
                        }
                    }
                }
                if (contractMode != 0 && extendedFac > 0.5f)
                {
                    extendedFac -= 1f / 120f;
                }
                switch (contractMode)
                {
                    case ContractMode.Even:
                        extendedFac -= contractSpeed;
                        break;
                    case ContractMode.Jerky:
                        if (UnityEngine.Random.value < 0.0016666667f)
                        {
                            extendedFac -= 1f / Mathf.Lerp(4f, 30f, UnityEngine.Random.value);
                        }
                        break;
                    case ContractMode.Jammed:
                        if (UnityEngine.Random.value < 0.0007142857f)
                        {
                            contractMode = ContractMode.Jerky;
                        }
                        break;
                }
            }
            lightnessBonus = Mathf.Max(lightnessBonus - 0.1f, 0f);
            if (lightnessBonus == 0f)
            {
                saturationBonus = Mathf.Max(saturationBonus - 0.02f, 0f);
            }
            forcedAlpha = Mathf.Lerp(forcedAlpha, 0f, 0.05f);
            ConnectToPoint(ConnectedPos, CurrentLength, push: true, 0f, PreviousChunk.vel, 0.3f, 0f);
            if (terrainContact)
            {
                terrainContactTimer++;
            }
            else
            {
                terrainContactTimer = 0;
            }
            Vector2 vector3 = 0.25f * vel;
            PushOutOfTerrain(kGraphics.owner.room, ConnectedPos);
            if (terrainContact && terrainContactTimer > 4)
            {
                if (vultureCat.IsMiros)
                {
                    //kGraphics.owner.room.PlaySound((Random.value < 0.5f) ? SoundID.Spear_Fragment_Bounce : SoundID.Spear_Bounce_Off_Wall, pos, Mathf.InverseLerp(10f, 60f, vector3.magnitude), Mathf.Lerp(3.5f, 0.5f, Mathf.InverseLerp(7f, 70f, CurrentLength)));
                }
                else
                {
                    //kGraphics.owner.room.PlaySound(SoundID.Vulture_Feather_Hit_Terrain, pos, Mathf.InverseLerp(0.2f, 20f, vector3.magnitude), Mathf.Lerp(3.5f, 0.5f, Mathf.InverseLerp(7f, 70f, CurrentLength)));
                }
                terrainContactTimer = 0;
            }
        }

        public Color CurrentColor()
        {
            if (vultureCat.IsMiros)
            {
                Color rgb = HSLColor.Lerp(new HSLColor(vultureCat.ColorB.hue, Mathf.Lerp(vultureCat.ColorB.saturation, 1f, saturationBonus), Mathf.Lerp(vultureCat.ColorB.lightness, 1f, lightnessBonus)), vultureCat.ColorA, Mathf.Cos(Mathf.Pow(wingPosition, 0.75f) * (float)Math.PI)).rgb;
                rgb.a = Mathf.Max(0.4f, forcedAlpha, Mathf.Lerp(0.4f, 0.8f, Mathf.Cos(Mathf.Pow(wingPosition, 1.7f) * (float)Math.PI))) * (extendedFac + wing.flyingMode) * 0.5f * (1f - brokenColor);
                if (vultureCat.isLaserActive())
                {
                    rgb.a = UnityEngine.Random.value;
                }
                return rgb;
            }
            HSLColor colorB = vultureCat.ColorB;
            HSLColor colorA = vultureCat.ColorA;
            if (vultureCat.albino)
            {
                colorB.saturation = Mathf.Lerp(colorB.saturation, 1f, 0.2f);
                colorB.hue = 0f;
                colorB.lightness = Mathf.Lerp(colorB.saturation, 0.2f, 0.8f);
                colorA.saturation = 0.8f;
                colorA.lightness = 0.6f;
            }
            Color rgb2 = HSLColor.Lerp(new HSLColor(colorB.hue, 
                                                    Mathf.Lerp(colorB.saturation, 1f, saturationBonus), 
                                                    Mathf.Lerp(colorB.lightness, 1f, lightnessBonus)), 
                                       colorA, 
                                       Mathf.Cos(Mathf.Pow(wingPosition, 0.75f) * (float)Math.PI)).rgb;
            rgb2.a = Mathf.Max(forcedAlpha, Mathf.Lerp(0.2f, 0.6f, Mathf.Cos(Mathf.Pow(wingPosition, 1.7f) * (float)Math.PI))) * (extendedFac + wing.flyingMode) * 0.5f * (1f - brokenColor);
            return rgb2;
        }

        public float GetTentacleAngle(int index, int side)
        {
            if (index == 0 && side % 2 == 0)
            {
                return 1f;
            }
            if (index == 0 && side == 1)
            {
                return -1f;
            }
            if (index == 1 && side % 2 == 0)
            {
                return 4f;
            }
            return -4f;
        }
    }

    internal class RoyalWingTentacle : Tentacle
    {
        public class Mode : ExtEnum<Mode>
        {
            public static readonly Mode Climb = new Mode("Climb", register: true);

            public static readonly Mode Fly = new Mode("Fly", register: true);

            public Mode(string value, bool register = false)
                : base(value, register)
            {
            }
        }

        public RoyalWings vultureCat;
        private DebugSprite[] grabGoalSprites;

        public int index;
        public int side;
        public Mode mode;
        public Vector2 desiredGrabPos;
        private bool attachedAtTip;
        public int segmentsGrippingTerrain;
        public int framesWithoutReaching;
        public int otherTentacleIsFlying;
        public int grabDelay;
        public int framesOfHittingTerrain;
        public bool playGrabSound;
        public int stun;
        private float fm;
        public StaticSoundLoop wooshSound;
        private List<IntVector2> scratchPath;

        public Player player => owner as Player;

        public float tentacleDir
        {
            get
            {
                if (index == 0 && side % 2 == 0)
                {
                    return -1f;
                }
                if (!vultureCat.IsMiros)
                {
                    return 1f;
                }
                if (index == 0 && side == 1)
                {
                    return 1f;
                }
                if (index == 1 && side % 2 == 0)
                {
                    return -4f;
                }
                return 4f;
            }
        }

        public bool hasAnyGrip
        {
            get
            {
                if (!attachedAtTip)
                {
                    return segmentsGrippingTerrain > 0;
                }
                return true;
            }
        }

        public float flyingMode
        {
            get
            {
                return fm;
            }
            set
            {
                fm = Mathf.Clamp(value, 0f, 1f);
            }
        }

        private RoyalWingTentacle OtherTentacle => vultureCat.wings[index, 1 - side];

        #region 翅膀、羽毛的尺寸
        public float TentacleContour(float x)
        {
            float num = Mathf.Lerp(0.45f, 0.1f, flyingMode);
            float num2 = Mathf.Lerp(0.51f, 0.25f, flyingMode);
            float num3 = Mathf.Lerp(0.85f, 0.4f, flyingMode);
            float num4 = Mathf.Lerp(6.5f, 5.5f, flyingMode);
            float num5 = Mathf.Lerp(0.5f, 0.35f, flyingMode);
            float num6 = Mathf.Lerp(0.85f, 0f, flyingMode);
            float num7 = num6 + (1f - num6) * Mathf.Cos(Mathf.InverseLerp(num2, 1.2f, x) * (float)Math.PI * 0.5f);
            float num8 = (vultureCat.IsKing ? 1.2f : 1f);
            if (x < num)
            {
                return vultureCat.wingWidth * num4 * num5 * num8;
            }
            if (x < num2)
            {
                return vultureCat.wingWidth * num4 * Mathf.Lerp(num5, 1f, Custom.SCurve(Mathf.InverseLerp(num, num2, x), 0.1f)) * num8;
            }
            if (x < num3)
            {
                return vultureCat.wingWidth * num4 * num7 * num8;
            }
            return vultureCat.wingWidth * num4 * Mathf.Lerp(0.5f, 1f, Mathf.Cos(Mathf.Pow(Mathf.InverseLerp(num3, 1f, x), 4f) * (float)Math.PI * 0.5f)) * num7 * num8;
        }

        public float FeatherContour(float x)
        {
            return FeatherContour(x, flyingMode);
        }

        public static float FeatherContour(float x, float k)
        {
            float num = Mathf.Lerp(0.2f, 1f, Custom.SCurve(Mathf.Pow(x, 1.5f), 0.1f));
            if (Mathf.Pow(x, 1.5f) > 0.5f)
            {
                num *= Mathf.Sqrt(1f - Mathf.Pow(Mathf.InverseLerp(0.5f, 1f, Mathf.Pow(x, 1.5f)), 4.5f));
            }
            float num2 = 1f;
            num2 *= Mathf.Pow(Mathf.Sin(Mathf.Pow(x, 0.5f) * (float)Math.PI), 0.7f);
            if (x < 0.3f)
            {
                num2 *= Mathf.Lerp(0.7f, 1f, Custom.SCurve(Mathf.InverseLerp(0f, 0.3f, x), 0.5f));
            }
            return Mathf.Lerp(num * 0.5f, num2, k);
        }

        public static float FeatherWidth(float x)
        {
            return Mathf.Pow(Mathf.Sin(Mathf.InverseLerp(-0.45f, 1f, x) * (float)Math.PI), 2.6f);
        }
        #endregion

        public RoyalWingTentacle(Player player, BodyChunk chunk, float length, int index, int side, RoyalWings vultureCat)
            : base(player, chunk, length)
        {
            this.index = index;
            this.side = side;
            this.vultureCat = vultureCat;
            this.room = player.room;
            this.grabPath = new List<IntVector2>();
            this.segments = new List<IntVector2>();
            for (int i = 0; i < (int)(this.idealLength / 20f); i++)
            {
                this.segments.Add(player.abstractCreature.pos.Tile);//room.GetTilePosition(this.owner.firstChunk.pos)
            }
            tProps = new TentacleProps(stiff: false, rope: false, shorten: true, 0.5f, 0f, 0.2f, 1.2f, 0.2f, 1.2f, 10f, 0.25f, 5f, 15, 60, 12, 0);
            tChunks = new TentacleChunk[vultureCat.IsKing ? 10 : 8];
            for (int i = 0; i < tChunks.Length; i++)
            {
                tChunks[i] = new TentacleChunk(this, i, (float)(i + 1) / (float)tChunks.Length, vultureCat.wingWidth);
                tChunks[i].PhaseToSegment();
                tChunks[i].Reset();
            }
            mode = Mode.Climb;
            debugViz = false; 
            this.wooshSound = new StaticSoundLoop(SoundID.Vulture_Wing_Woosh_LOOP, base.Tip.pos, room, 1f, 1f);
        }

        public override void NewRoom(Room room)
        {
            /*//idealLength < 20f将导致segments不存在元素
            idealLength = Mathf.Lerp(vultureCat.wingLength * (vultureCat.IsKing ? 9f : 7f),
                                     vultureCat.wingLength * (vultureCat.IsKing ? 13.5f : 11f),
                                     flyingMode);*/
            base.NewRoom(room);
            wooshSound = new StaticSoundLoop(SoundID.Vulture_Wing_Woosh_LOOP, base.Tip.pos, room, 1f, 1f);
            if (debugViz)
            {
                if (grabGoalSprites != null)
                {
                    grabGoalSprites[0].RemoveFromRoom();
                    grabGoalSprites[1].RemoveFromRoom();
                }
                grabGoalSprites = new DebugSprite[2];
                grabGoalSprites[0] = new DebugSprite(new Vector2(0f, 0f), new FSprite("pixel"), room);
                grabGoalSprites[0].sprite.scale = 10f;
                grabGoalSprites[0].sprite.color = new Color(1f, 0f, 0f);
                grabGoalSprites[1] = new DebugSprite(new Vector2(0f, 0f), new FSprite("pixel"), room);
                grabGoalSprites[1].sprite.scale = 10f;
                grabGoalSprites[1].sprite.color = new Color(0f, 5f, 0f);
            }
        }

        public override void Update()
        {
            base.Update();
            if (player.enteringShortCut.HasValue)
            {
                base.retractFac = Mathf.Min(0f, base.retractFac - 0.1f);
                for (int i = 0; i < tChunks.Length; i++)
                {
                    tChunks[i].vel += Vector2.ClampMagnitude(room.MiddleOfTile(player.enteringShortCut.Value) - tChunks[i].pos, 50f) / 10f;
                }
                if (segments.Count > 1)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
                return;
            }
            attachedAtTip = false;
            idealLength = Mathf.Lerp(vultureCat.wingLength * (vultureCat.IsKing ? 9f : 7f), 
                                     vultureCat.wingLength * (vultureCat.IsKing ? 13.5f : 11f), 
                                     flyingMode);
            if (stun > 0)
            {
                stun--;
            }
            if (Mathf.Pow(UnityEngine.Random.value, 0.25f) > 2f * vultureCat.wingHealth[index, side])
            {
                stun = Math.Max(stun, (int)Mathf.Lerp(-2f, 12f, Mathf.Pow(UnityEngine.Random.value, 0.5f + 20f * Mathf.Max(0f, vultureCat.wingHealth[index, side]))));
            }
            limp = stun > 0;//!vultureCat.Consious || 
            if (limp)
            {
                floatGrabDest = null;
                for (int j = 0; j < tChunks.Length; j++)
                {
                    tChunks[j].vel *= 0.9f;
                    tChunks[j].vel.y -= 0.5f;
                }
            }
            for (int k = 0; k < tChunks.Length; k++)
            {
                tChunks[k].rad = TentacleContour(tChunks[k].tPos);
                if (backtrackFrom == -1 || k < backtrackFrom)
                {
                    if (k > 1 && Custom.DistLess(tChunks[k].pos, tChunks[k - 2].pos, 30f))
                    {
                        tChunks[k].vel -= Custom.DirVec(tChunks[k].pos, tChunks[k - 2].pos) * (30f - Vector2.Distance(tChunks[k].pos, tChunks[k - 2].pos)) * 0.1f;
                    }
                    else if (k <= 1)
                    {
                        tChunks[k].vel = Custom.DirVec(OtherTentacle.connectedChunk.pos, connectedChunk.pos) * ((k == 0) ? 2f : 1.2f);
                    }
                }
                if (room.PointSubmerged(tChunks[k].pos))
                {
                    tChunks[k].vel *= 0.5f;
                }
                if (tChunks[k].contactPoint.x != 0 && tChunks[k].lastContactPoint.x == 0 && Mathf.Abs(tChunks[k].pos.x - tChunks[k].lastPos.x) > 6f)
                {
                    room.PlaySound(SoundID.Vulture_Tentacle_Collide_Terrain, tChunks[k].pos, 0.25f * Mathf.InverseLerp(6f, 16f, Mathf.Abs(tChunks[k].pos.x - tChunks[k].lastPos.x)), 1f);
                }
                else if (tChunks[k].contactPoint.y != 0 && tChunks[k].lastContactPoint.y == 0 && Mathf.Abs(tChunks[k].pos.y - tChunks[k].lastPos.y) > 6f)
                {
                    room.PlaySound(SoundID.Vulture_Tentacle_Collide_Terrain, tChunks[k].pos, 0.25f * Mathf.InverseLerp(6f, 16f, Mathf.Abs(tChunks[k].pos.y - tChunks[k].lastPos.y)), 1f);
                }
            }
            if (!limp)
            {
                if (mode == Mode.Climb)
                {
                    if (floatGrabDest.HasValue && Custom.DistLess(tChunks[tChunks.Length - 1].pos, floatGrabDest.Value, 40f) && backtrackFrom == -1)
                    {
                        tChunks[tChunks.Length - 1].pos = floatGrabDest.Value;
                        tChunks[tChunks.Length - 1].vel *= 0f;
                        attachedAtTip = true;
                    }
                    flyingMode -= 0.025f;
                    base.Tip.collideWithTerrain = !attachedAtTip;
                    UpdateDesiredGrabPos();
                    bool flag = side % 2 == 0;
                    BodyChunk bodyChunk = player.bodyChunks[0];//(flag ? vultureCat.bodyChunks[3] : vultureCat.bodyChunks[2]);
                    segmentsGrippingTerrain = 0;
                    for (int l = 0; l < tChunks.Length; l++)
                    {
                        tChunks[l].vel *= Mathf.Lerp(0.95f, 0.85f, Support());
                        if (attachedAtTip && (backtrackFrom == -1 || l < backtrackFrom) && GripTerrain(l))
                        {
                            segmentsGrippingTerrain++;
                            for (int num = l - 1; num > 0; num--)
                            {
                                PushChunksApart(l, num);
                            }
                        }
                        else
                        {
                            tChunks[l].vel.y += 0.1f;
                            tChunks[l].vel += connectedChunk.vel * 0.1f;
                            if (!hasAnyGrip)
                            {
                                if (floatGrabDest.HasValue)
                                {
                                    tChunks[l].vel += Custom.DirVec(tChunks[l].pos, floatGrabDest.Value) * 0.3f;
                                }
                                else
                                {
                                    tChunks[l].vel += Custom.DirVec(tChunks[l].pos, desiredGrabPos + Custom.DirVec(base.FloatBase, desiredGrabPos) * 70f) * 0.6f;
                                }
                            }
                        }
                        tChunks[l].vel += Custom.DirVec(bodyChunk.pos, tChunks[l].pos) * 0.5f / ((float)l + 1f);
                    }
                    if (attachedAtTip)
                    {
                        framesWithoutReaching = 0;
                        if (SharedPhysics.RayTraceTilesForTerrain(room, base.BasePos, base.grabDest.Value))
                        {
                            if (//!Custom.DistLess(base.Tip.pos, connectedChunk.pos, idealLength)
                                Custom.DistLess(vultureCat.wantPos, connectedChunk.pos, 20f) &&
                                Vector2.Dot(vultureCat.wantPos - connectedChunk.pos, base.Tip.pos - connectedChunk.pos) < 0 &&
                                !Custom.DistLess(base.Tip.pos, connectedChunk.pos, idealLength * 0.9f) &&
                                Custom.DistLess(base.Tip.pos, connectedChunk.pos, idealLength * 1.0f + 15f))
                            {
                                Vector2 vector = Custom.DirVec(base.Tip.pos, connectedChunk.pos);
                                float num2 = Vector2.Distance(base.Tip.pos, connectedChunk.pos);
                                float num3 = idealLength * 0.9f;
                                connectedChunk.pos += vector * (num3 - num2) * 0.2f;
                                connectedChunk.vel += vector * (num3 - num2) * 0.2f;
                            }/*
                            if (!Custom.DistLess(base.Tip.pos, connectedChunk.pos, idealLength * 0.9f))
                            {
                                vultureCat.hangingInTentacle = true;
                            }*/
                            if(!Custom.DistLess(base.Tip.pos, connectedChunk.pos, idealLength * vultureCat.groundRetractionScale)// && UnderneathIsSolid()
                                )
                            {
                                ReleaseGrip();
                                vultureCat.hangingInTentacle = false;
                            }
                        }
                        if (playGrabSound)
                        {
                            room.PlaySound(SoundID.Vulture_Tentacle_Grab_Terrain, base.Tip.pos, 0.25f, 1f);
                            playGrabSound = false;
                        }
                    }
                    /*//允许伸翅膀
                    else if (!Custom.DistLess(connectedChunk.pos, vultureCat.wantPos, 5f) && 
                             OtherTentacle.attachedAtTip)
                    {
                        tChunks[tChunks.Length - 1].vel = Vector2.Lerp(tChunks[tChunks.Length - 1].vel, vultureCat.wantPos - tChunks[tChunks.Length - 1].pos, 0.05f);
                    }*/
                    else
                    {
                        playGrabSound = true;
                        FindGrabPos(ref scratchPath);
                        framesWithoutReaching++;
                        if ((float)framesWithoutReaching > 60f && !floatGrabDest.HasValue)
                        {/*
                            Vector2 down = Vector2.down * idealLength * (UnderneathIsSolid() ? 1f : 0f);
                            tChunks[tChunks.Length - 1].vel = Vector2.Lerp(tChunks[tChunks.Length - 1].vel, connectedChunk.pos + down - tChunks[tChunks.Length - 1].pos, 0.05f);*/
                            framesWithoutReaching = 0;
                            //SwitchMode(Mode.Fly);
                        }
                    }
                    if (OtherTentacle.mode == Mode.Fly)
                    {
                        otherTentacleIsFlying++;
                        if (!hasAnyGrip && ((otherTentacleIsFlying > 30 && room.aimap.getTerrainProximity(base.BasePos) >= 3) || otherTentacleIsFlying > 100))
                        {
                            SwitchMode(Mode.Fly);
                            vultureCat.InitiateFlight(player);
                            otherTentacleIsFlying = 0;
                        }
                    }
                    else
                    {
                        otherTentacleIsFlying = 0;
                    }
                }
                else if (mode == Mode.Fly)
                {
                    bool contact = false;
                    flyingMode += 0.05f;
                    for (int m = 0; m < tChunks.Length; m++)
                    {
                        tChunks[m].vel *= 0.95f;
                        tChunks[m].vel.x += tentacleDir * 0.6f;
                        bool shouldBeMirrored = side % 2 == 0;
                        Vector2 wantDir = Vector2.Lerp(Custom.PerpendicularVector(Custom.DirVec(player.bodyChunks[0].pos, player.bodyChunks[1].pos)) * (shouldBeMirrored ? (-1f) : 1f),
                                                       new Vector2(tentacleDir, 0f),
                                                       0.5f);
                        Vector2 wantPos = connectedChunk.pos + idealLength * Mathf.Pow(tChunks[m].tPos, Mathf.Sqrt(vultureCat.wingLength / 20f)) * wantDir;
                        Vector2 perp = Custom.PerpendicularVector((connectedChunk.pos - wantPos).normalized) * (shouldBeMirrored ? (-1f) : 1f);
                        float wave = Mathf.Sin((float)Math.PI * 2f * (vultureCat.wingFlap - tChunks[m].tPos * 0.5f));
                        float waveScale = Mathf.Lerp(Mathf.Pow(1 - tChunks[m].tPos, 1 - Mathf.Sqrt(vultureCat.wingLength / 20f)), 1f, 0.5f) * 
                                          vultureCat.wingLength * Mathf.Lerp(10f, 30f, vultureCat.wingFlapAmplitude);
                        waveScale *= player.input[0].y < 0 ? 0f : 1f;
                        waveScale *= 1f + player.mainBodyChunk.vel.magnitude / 10f;
                        wantPos += perp * waveScale * wave;
                        tChunks[m].vel += Vector2.ClampMagnitude(wantPos - tChunks[m].pos, 1.5f * vultureCat.wingLength) / (1.5f * vultureCat.wingLength) * 5f * Mathf.Lerp(0.2f, 1f, vultureCat.wingFlapAmplitude);
                        if (tChunks[m].contactPoint.x != 0 || tChunks[m].contactPoint.y != 0)
                        {
                            contact = true;
                        }
                    }
                    float num4 = 0.5f;
                    if (vultureCat.IsMiros)
                    {
                        num4 = 1.4f / (float)vultureCat.wings.Length;
                    }
                    player.bodyChunks[0].vel.y += vultureCat.wingLength / 20f * Mathf.Pow(num4 + num4 * Mathf.Sin((float)Math.PI * 2f * vultureCat.wingFlap), 2f) * 5.6f * Mathf.Lerp(0.5f, 1f, vultureCat.wingFlapAmplitude);
                    player.bodyChunks[0].vel.x += vultureCat.wingLength / 20f * (num4 + num4 * Mathf.Sin((float)Math.PI * 2f * vultureCat.wingFlap)) * -2.6f * tentacleDir * Mathf.Lerp(0.5f, 1f, vultureCat.wingFlapAmplitude);
                    if (OtherTentacle.stun > 0 && stun < 1)
                    {
                        for (int n = 0; n < player.bodyChunks.Length; n++)
                        {
                            player.bodyChunks[n].vel += vultureCat.wingLength / 20f * Custom.DirVec(base.Tip.pos, player.bodyChunks[n].pos) * Mathf.Pow(num4 + num4 * Mathf.Sin((float)Math.PI * 2f * vultureCat.wingFlap), 2f) * 0.4f * Mathf.Lerp(0.5f, 1f, vultureCat.wingFlapAmplitude);
                        }
                    }
                    if (contact)
                    {
                        framesOfHittingTerrain++;
                    }
                    else
                    {
                        framesOfHittingTerrain--;
                    }
                    framesOfHittingTerrain = Custom.IntClamp(framesOfHittingTerrain, 0, 30);/*
                    if (framesOfHittingTerrain >= 30)
                    {
                        framesOfHittingTerrain = 0;
                        SwitchMode(Mode.Climb);
                    }
                    else if (OtherTentacle.mode == Mode.Climb && OtherTentacle.attachedAtTip)
                    {
                        UpdateDesiredGrabPos();
                        FindGrabPos(ref scratchPath);
                        if (floatGrabDest.HasValue)
                        {
                            SwitchMode(Mode.Climb);
                        }
                    }*/
                }
            }
            wooshSound.volume = Custom.SCurve(Mathf.InverseLerp(0.4f, 18f, Vector2.Distance(base.Tip.pos - connectedChunk.pos, base.Tip.lastPos - connectedChunk.lastPos)), 0.6f) * flyingMode;
            wooshSound.pitch = Mathf.Lerp(0.3f, 1.7f, Mathf.InverseLerp(-20f, 20f, base.Tip.lastPos.y - base.Tip.pos.y - (connectedChunk.lastPos.y - connectedChunk.pos.y)));
            wooshSound.pos = Vector2.Lerp(connectedChunk.pos, base.Tip.pos, 0.7f);
            wooshSound.Update();
            if (debugViz)
            {
                grabGoalSprites[1].pos = desiredGrabPos;
            }
        }

        public void SwitchMode(Mode newMode)
        {
            mode = newMode;
            if (newMode == Mode.Fly)
            {
                if (vultureCat.IsMiros)
                {
                    ReleaseGrip();
                }
                floatGrabDest = null;
            }
        }

        public void ReleaseGrip()
        {
            if (OtherTentacle.grabDelay < 1)
            {
                grabDelay = 10;// 10;
            }
            floatGrabDest = null;
        }

        private void UpdateDesiredGrabPos()
        {
            if (vultureCat.hoverStill)
            {
                desiredGrabPos = player.mainBodyChunk.pos +
                                 (UnderneathIsSolid() ? 0f : 0f) * Vector2.down * idealLength +
                                 (UnderneathIsSolid() ? 0.7f : 0.7f) * new Vector2(tentacleDir, -0.8f).normalized * idealLength;
            }
            else
            {
                desiredGrabPos = player.mainBodyChunk.pos +
                                 (UnderneathIsSolid() ? 0f : 0f) * Vector2.down * idealLength +
                                 (UnderneathIsSolid() ? 0.7f : 0.7f) * (Vector2)Vector3.Slerp(vultureCat.moveDirection, new Vector2(tentacleDir, -0.8f).normalized, 0.3f) * idealLength;
            }
        }

        private void FindGrabPos(ref List<IntVector2> path)
        {
            if (grabDelay > 0)
            {
                grabDelay--;
                return;
            }
            IntVector2? intVector = ClosestSolid(room.GetTilePosition(desiredGrabPos), 8, 8f);
            if (intVector.HasValue)
            {
                IntVector2? intVector2 = SharedPhysics.RayTraceTilesForTerrainReturnFirstSolid(room, base.BasePos, intVector.Value);
                if (!base.grabDest.HasValue || GripPointAttractiveness(intVector2.Value) > GripPointAttractiveness(base.grabDest.Value))
                {
                    Vector2 newGrabDest = Custom.RestrictInRect(base.FloatBase, FloatRect.MakeFromVector2(room.MiddleOfTile(intVector2.Value) - new Vector2(11f, 11f), room.MiddleOfTile(intVector2.Value) + new Vector2(11f, 11f)));
                    MoveGrabDest(newGrabDest, ref path);
                }
            }
            Vector2 pos = desiredGrabPos + Custom.DegToVec(UnityEngine.Random.value * 360f) * UnityEngine.Random.value * idealLength;
            int num = room.RayTraceTilesList(base.BasePos.x, base.BasePos.y, room.GetTilePosition(pos).x, room.GetTilePosition(pos).y, ref path);
            for (int i = 0; i < num && !room.GetTile(path[i]).Solid; i++)
            {
                if ((room.GetTile(path[i]).horizontalBeam || room.GetTile(path[i]).verticalBeam) && (!base.grabDest.HasValue || GripPointAttractiveness(path[i]) > GripPointAttractiveness(base.grabDest.Value)))
                {
                    MoveGrabDest(room.MiddleOfTile(path[i]), ref path);
                    break;
                }
            }
        }

        public float ReleaseScore()
        {
            if (mode != Mode.Climb)
            {
                return float.MinValue;
            }
            float num = Vector2.Distance(base.Tip.pos, desiredGrabPos);
            if (!floatGrabDest.HasValue)
            {
                num *= 2f;
            }
            return num;
        }

        private bool UnderneathIsSolid()
        {
            float num = Custom.AimFromOneVectorToAnother(player.bodyChunks[1].pos, player.bodyChunks[0].pos);
            bool result = player.bodyChunks[0].ContactPoint.y < 0 || player.bodyChunks[1].ContactPoint.y < 0 ||
                          player.room.GetTile(player.bodyChunks[0].pos + 20f * Mathf.Abs(Mathf.Sin(num)) * Vector2.down).Terrain == Room.Tile.TerrainType.Solid ||
                          player.room.GetTile(player.bodyChunks[0].pos + 20f * Mathf.Abs(Mathf.Sin(num)) * Vector2.down).Terrain == Room.Tile.TerrainType.Slope ||
                          player.room.GetTile(player.bodyChunks[0].pos + 20f * Mathf.Abs(Mathf.Sin(num)) * Vector2.down).Terrain == Room.Tile.TerrainType.Floor;
            return result;
        }

        private bool GripTerrain(int chunk)
        {
            for (int i = 0; i < 4; i++)
            {
                if (room.GetTile(room.GetTilePosition(tChunks[chunk].pos) + Custom.fourDirections[i]).Solid)
                {
                    tChunks[chunk].vel *= 0.25f;
                    tChunks[chunk].vel += Custom.fourDirections[i].ToVector2() * 0.8f;
                    if (tChunks[chunk].contactPoint.x == 0)
                    {
                        return tChunks[chunk].contactPoint.y != 0;
                    }
                    return true;
                }
            }
            if (room.GetTile(tChunks[chunk].pos).horizontalBeam)
            {
                tChunks[chunk].vel *= 0.25f;
                tChunks[chunk].vel.y += (room.MiddleOfTile(tChunks[chunk].pos).y - tChunks[chunk].pos.y) * 0.3f;
                return true;
            }
            if (room.GetTile(tChunks[chunk].pos).verticalBeam)
            {
                tChunks[chunk].vel *= 0.25f;
                tChunks[chunk].vel.x += (room.MiddleOfTile(tChunks[chunk].pos).x - tChunks[chunk].pos.x) * 0.3f;
                return true;
            }
            return false;
        }

        private float GripPointAttractiveness(IntVector2 pos)
        {
            if (room.GetTile(pos).Solid)
            {
                return 100f / room.GetTilePosition(desiredGrabPos).FloatDist(pos);
            }
            return 65f / room.GetTilePosition(desiredGrabPos).FloatDist(pos);
        }

        public float Support()
        {
            if (stun > 0)
            {
                return 0f;
            }
            if (mode == Mode.Climb)
            {
                return Mathf.Clamp(((!hasAnyGrip) ? 0f : (vultureCat.IsMiros ? 4f : 0.5f)) + (float)segmentsGrippingTerrain / (float)tChunks.Length, 0f, 1f);
            }
            if (mode == Mode.Fly)
            {
                if (!vultureCat.IsMiros)
                {
                    return 0.5f;
                }
                return 1.2f;
            }
            return 0f;
        }

        private IntVector2? ClosestSolid(IntVector2 goal, int maxDistance, float maxDistFromBase)
        {
            if (room.GetTile(goal).Solid)
            {
                return goal;
            }
            for (int i = 1; i <= maxDistance; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (room.GetTile(goal + Custom.eightDirections[j] * i).Solid && base.BasePos.FloatDist(goal + Custom.eightDirections[j] * i) < maxDistFromBase)
                    {
                        return goal + Custom.eightDirections[j] * i;
                    }
                }
            }
            return null;
        }

        public bool WingSpace()
        {
            for (int i = -1; i <= 1; i++)
            {
                if (!SharedPhysics.RayTraceTilesForTerrain(room, room.GetTilePosition(connectedChunk.pos), room.GetTilePosition(connectedChunk.pos + new Vector2(tentacleDir * idealLength, 50f * (float)i))))
                {
                    return false;
                }
            }
            return true;
        }

        public override IntVector2 GravityDirection()
        {
            if (!(UnityEngine.Random.value < 0.5f))
            {
                return new IntVector2(0, -1);
            }
            return new IntVector2((int)tentacleDir, -1);
        }

        public void Damage(Creature.DamageType type, float damage, float stunBonus)
        {
            damage /= 2.7f;
            stunBonus /= 1.2f;
            stun = Math.Max(stun, (int)(damage * 30f + stunBonus));
            vultureCat.wingHealth[index, side] -= damage;
        }
    }
}

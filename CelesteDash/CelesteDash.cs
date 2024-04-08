using Modding;
using System;
using HKMirror;
using HKMirror.Reflection.SingletonClasses;
using UnityEngine;
using UnityEngine.Timeline;
using System.Diagnostics.Eventing.Reader;
using System.Collections.ObjectModel;
using System.EnterpriseServices;
using System.Collections.Generic;
using System.Xml.Linq;
using Satchel;
using Satchel.BetterMenus;
using System.Diagnostics;


namespace CelesteDash
{
    public class CelesteDashMod : Mod, ICustomMenuMod
    {
        private static CelesteDashMod? _instance;
        private static Vector2 dashDir;
        private static int dashFrames;
        private static int groundFrames;
        private static bool inHyper;
        private static bool enteredHyperJump;
        private static float maxDashSpeed;
        private static bool isDashJumpExtended;
        private static float recoilSpeed;
        public static string NO_SHADOW_DASH_BUTTON = "left shift";
        private bool isWallbouncing = false;
        private bool slashBoostOption = true;
        private bool allowBunnyHopOption;
        private bool canExceedSpeed = false;
        private const float EPS = 0.001f;
        private const float RECOILSPEED = 20f;
        private const float WALLBOUCEDIST = 1f;
        private const float MIDAIRDASHDIST = 0.35f;

        private Menu MenuRef;
        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? modtoggledelegates)
        {
            //Create a new MenuRef if it's not null
            MenuRef ??= new Menu(
                        name: "Celeste Dash Menu", //the title of the menu screen, it will appear on the top center of the screen 
                        elements: new Element[]
                        {
                            Blueprints.HorizontalBoolOption(
                                name: "Slash boost",
                                description: "allows to get speed when pogo a wall",
                                applySetting: (v) =>
                                {
                                    slashBoostOption = v;
                                    if (v) {recoilSpeed = RECOILSPEED; }
                                    else {recoilSpeed = 0f; }
                                },
                                loadSetting: () =>
                                {
                                    if (slashBoostOption) {recoilSpeed = RECOILSPEED; }
                                    else {recoilSpeed = 0f; }
                                    return slashBoostOption;
                                }),
                            Blueprints.HorizontalBoolOption(
                                name: "Bunnyhop",
                                description: "Don't loss speed if jumping repeatedly",
                                applySetting: (v) =>
                                {
                                    allowBunnyHopOption = v;
                                },
                                loadSetting: () =>
                                {
                                    return allowBunnyHopOption;
                                })
                        }
            );

            //uses the GetMenuScreen function to return a menuscreen that MAPI can use. 
            //The "modlistmenu" that is passed into the parameter can be any menuScreen that you want to return to when "Back" button or "esc" key is pressed 
            return MenuRef.GetMenuScreen(modListMenu);
        }
        public bool ToggleButtonInsideMenu { get; }

        public static float BtF(bool x)
        {
            if (x) { return 1f;  }
            return 0f;
        }
        internal static CelesteDashMod Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException($"An instance of {nameof(CelesteDashMod)} was never constructed");
                }
                return _instance;
            }
        }

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public CelesteDashMod() : base("CelesteDash")
        {

            _instance = this;
        }

        public override void Initialize()
        {
            Log("Initializing");
            On.HeroController.CanJump += CanJumpNoDash;
            On.HeroController.Dash += CDash;
            On.HeroController.CanDash += CanDash8Dir;
            On.HeroController.Move += CelesteMove;
            On.HeroController.Jump += CJump;
            //On.HeroController.CancelRecoilHorizontal += CCancelRecoulHz;
            ModHooks.HeroUpdateHook += CUpdate;
            On.HeroController.CanWallJump += CCanWallJump;
            On.HeroController.CancelJump += CCancelJump;
            inHyper = false;
            dashDir = Vector2.zero;
            groundFrames = 0;
            dashFrames = 0;
            isDashJumpExtended = true;
            maxDashSpeed = 0;
            recoilSpeed = RECOILSPEED;
            NO_SHADOW_DASH_BUTTON = "left shift";
            // put additional initialization logic here
            Log("Initialized");
        }





        /*
private void CCancelRecoulHz(On.HeroController.orig_CancelRecoilHorizontal orig, HeroController self)
{
   if (HeroControllerR.cState.recoilingLeft)
   {
       HeroControllerR.cState.recoilingLeft = false;
       HeroControllerR.rb2d.velocity = new Vector2(-recoilSpeed, HeroControllerR.rb2d.velocity.y);
   }
   if (HeroControllerR.cState.recoilingRight)
   {
       HeroControllerR.cState.recoilingRight = false;
       HeroControllerR.rb2d.velocity = new Vector2(recoilSpeed, HeroControllerR.rb2d.velocity.y);
   }
   HeroControllerR.cState.recoilingLeft = false;
   HeroControllerR.cState.recoilingRight = false;
   HeroControllerR.recoilSteps = 0;
}*/
        char canWallbounce()
        {
            Bounds heroBounds = HeroControllerR.col2d.bounds;
            for (int step = 0; step < 4; step++)
            {
                float curY = heroBounds.min.y + (heroBounds.max.y - heroBounds.min.y) * (step / 3f);
                Vector2 leftPos = new Vector2(heroBounds.min.x, curY);
                Vector2 rightPos = new Vector2(heroBounds.max.x, curY);
                UnityEngine.RaycastHit2D leftBounce = Physics2D.Raycast(leftPos, Vector2.left, WALLBOUCEDIST, 256);
                UnityEngine.RaycastHit2D rightBounce = Physics2D.Raycast(rightPos, Vector2.right, WALLBOUCEDIST, 256);
                if ((leftBounce.collider != null) && (leftBounce.collider.gameObject.GetComponent<NonSlider>() == null))
                {
                    return 'L';
                }
                if ((rightBounce.collider != null) && (rightBounce.collider.gameObject.GetComponent<NonSlider>() == null))
                {
                    return 'R';
                }
            }
            return 'N';
        }

        bool IsCloseToGround()
        {
            Bounds heroBounds = HeroControllerR.col2d.bounds;
            for (int step = 0; step < 4; step++)
            {
                float curX = heroBounds.min.x + (heroBounds.max.x - heroBounds.min.x) * (step / 3f);
                Vector2 curPos = new Vector2(curX, heroBounds.min.y);
                UnityEngine.RaycastHit2D downCollision = Physics2D.Raycast(curPos, Vector2.down, WALLBOUCEDIST, 256);
                if ((downCollision.collider != null) && (downCollision.collider.gameObject.GetComponent<NonSlider>() == null))
                {
                    return true;
                }
            }
            return false;
        }
        private bool CCanWallJump(On.HeroController.orig_CanWallJump orig, HeroController self)
        {
            if (!HeroControllerR.playerData.GetBool("hasWalljump")) { return false; }
            if (HeroControllerR.cState.touchingNonSlider)
            {
                return false;
            }
            if (HeroControllerR.cState.wallSliding)
            {
                return true;
            }

            if (HeroControllerR.cState.dashing && dashDir.y > EPS && Math.Abs(dashDir.x) < EPS) // dashing up
            {
                char wallbounceDir = canWallbounce();
                if (wallbounceDir == 'N') { return false; }
                if (wallbounceDir == 'L')
                {
                    HeroControllerR.touchingWallL = true;
                    isWallbouncing = true;
                    HeroControllerR.FinishedDashing();
                    return true;
                }
                if (wallbounceDir == 'R')
                {
                    HeroControllerR.touchingWallR = true;
                    isWallbouncing = true;
                    HeroControllerR.FinishedDashing();
                    return true;
                }
            }
            if (HeroControllerR.cState.touchingWall && !HeroControllerR.cState.onGround)
            {
                return true;
            }
            return false;
        }

        private void CUpdate()
        {

            
            if ((HeroControllerR.cState.recoilingLeft))
            {
                if (slashBoostOption) { canExceedSpeed = true; }
                HeroControllerR.rb2d.velocity = new Vector2(-recoilSpeed, HeroControllerR.rb2d.velocity.y);//recoilSpeed;
            }
            if (HeroControllerR.cState.recoilingRight)
            {
                if (slashBoostOption) { canExceedSpeed = true; }
                HeroControllerR.rb2d.velocity = new Vector2(recoilSpeed, HeroControllerR.rb2d.velocity.y);
            } 
            if ((HeroControllerR.cState.onGround || HeroControllerR.cState.wallSliding || HeroControllerR.cState.inAcid) && (HeroControllerR.dashCooldownTimer <= 0f)) ///refill on wallslide
            {
                isDashJumpExtended = true;
            }
        }
        private void CCancelJump(On.HeroController.orig_CancelJump orig, HeroController self)
        {
            HeroControllerR.cState.jumping = false;
            HeroControllerR.jumpReleaseQueuing = false;
            HeroControllerR.jump_steps = 0;
            isWallbouncing = false;
        }
        private void CJump(On.HeroController.orig_Jump orig, HeroController self)
        {
            if (HeroControllerR.jump_steps == 0)
            {
                enteredHyperJump = inHyper;
                inHyper = false;
            }
            if (enteredHyperJump && HeroControllerR.jump_steps * 2 > HeroControllerR.JUMP_STEPS)
            {
                enteredHyperJump = false;
                HeroControllerR.CancelJump();
                return;
            }
            if (HeroControllerR.jump_steps > HeroControllerR.JUMP_STEPS)
            {
                HeroControllerR.CancelJump();
                return;
            }
            Vector2 lastVel = HeroControllerR.rb2d.velocity;
            float newVelY = HeroControllerR.JUMP_SPEED;
            if (isWallbouncing)
            {
                if (HeroControllerR.jump_steps == 0)  // first time entered wallbounce
                    { newVelY = dashDir.y; }
                else 
                    { newVelY = Math.Max(newVelY, lastVel.y - Time.deltaTime * HeroControllerR.rb2d.gravityScale / 2); }
                
            }
            HeroControllerR.rb2d.velocity = new Vector2(lastVel.x, newVelY);
            /*
            if (!isWallbouncing)
                { HeroControllerR.rb2d.velocity = new Vector2(HeroControllerR.rb2d.velocity.x, HeroControllerR.JUMP_SPEED); }
            else 
                { HeroControllerR.rb2d.velocity = new Vector2(HeroControllerR.rb2d.velocity.x, dashDir.y); }
            */
            HeroControllerR.jump_steps++;
            HeroControllerR.jumped_steps++;
            HeroControllerR.ledgeBufferSteps = 0;
        }

        private void CDash(On.HeroController.orig_Dash orig, HeroController self)
        {
            if (HeroControllerR.dash_timer < EPS) // first time entering dash
            {
                float dashAngle = Vector2.SignedAngle(dashDir, new Vector2(1f, 0f));
                Vector2 particleDashDir = -dashDir.normalized * 3.74f;
                HeroControllerR.dashBurst.transform.localPosition = new Vector3(Math.Abs(particleDashDir.x), particleDashDir.y, 0.01f);
                //HeroControllerR.dashBurst.transform.localPosition = new Vector3(-0.07f, 3.74f, 0.01f);
                if (dashDir.x < EPS) { dashAngle = 180f - dashAngle; }
                HeroControllerR.dashBurst.transform.localEulerAngles = new Vector3(0f, 0f, dashAngle);

                if (dashDir.y > EPS) { HeroControllerR.airDashed = true; }
            }
            groundFrames = 0;
            dashFrames++;
            HeroControllerR.AffectedByGravity(gravityApplies: false);
            HeroControllerR.ResetHardLandingTimer();
            if (HeroControllerR.dash_timer > HeroControllerR.DASH_TIME)
            {
                inHyper = false;
                
                HeroControllerR.FinishedDashing();
                //dashDir = new Vector2(0f, 0f); ///intended ultra
                if (dashDir.y > -0.001f)
                {
                    HeroControllerR.rb2d.velocity = new Vector2(0f, 0f);
                }
                if (dashDir.y > 0.001f)
                {
                    HeroControllerR.rb2d.velocity = new Vector2(0f, 1f);
                }
                //dashDir = Vector2.zero; intended ultra
                return;
            }

            if ((HeroControllerR.dash_timer < EPS) || (dashDir.y > EPS)) // first time entering dash
                { HeroControllerR.rb2d.velocity = dashDir; }
            if ((!HeroControllerR.cState.onGround) && (dashDir.y < -EPS) && (Math.Abs(HeroControllerR.rb2d.velocity.y) < EPS)) //diagonal dash hit ground and left it
            {
                Vector2 lastVel = HeroControllerR.rb2d.velocity;
                HeroControllerR.FinishedDashing();
                HeroControllerR.rb2d.velocity = lastVel;
                return;
            }
            if (HeroControllerR.cState.onGround)
            {
                isDashJumpExtended = true;
                if (dashDir.y < -0.001f)
                {

                    HeroControllerR.rb2d.velocity = new Vector2(dashDir.x, 0f);// * (float)Math.Sqrt(2), 0f);
                }
            }
            HeroControllerR.dash_timer += Time.deltaTime;
        }

        private void CelesteMove(On.HeroController.orig_Move orig, HeroController self, float move_direction)
        {
            ///TODO: set grounded actor state
            if (HeroControllerR.cState.onGround)
            {
                HeroControllerR.SetState(GlobalEnums.ActorStates.grounded);
            }
            //orig(self, move_direction);/// temp replace for grounded state
            if (!HeroControllerR.acceptingInput || HeroControllerR.cState.wallSliding) { return; }
            if (HeroControllerR.cState.inWalkZone) {
                HeroControllerR.current_velocity = new Vector2(move_direction * HeroControllerR.WALK_SPEED, HeroControllerR.current_velocity.y);
            }
            ///TODO: charm 31, 37
            float maxRunSpeed = HeroControllerR.RUN_SPEED;
            if (HeroControllerR.cState.inAcid) { maxRunSpeed = HeroControllerR.UNDERWATER_SPEED; }
            else if (HeroControllerR.playerData.GetBool("equippedCharm_37") &&
                HeroControllerR.playerData.GetBool("equippedCharm_31") && HeroControllerR.cState.onGround)
                    { maxRunSpeed = HeroControllerR.RUN_SPEED_CH_COMBO; }
            else if (HeroControllerR.playerData.GetBool("equippedCharm_37") && HeroControllerR.cState.onGround)
                    { maxRunSpeed = HeroControllerR.RUN_SPEED_CH; }
            float stoppingSpeed = 2f;
            float groundFriction = 0.8f;
            Vector2 new_velocity = HeroControllerR.current_velocity;

            if (HeroControllerR.cState.onGround) {// ground friction
                groundFrames += 1;
                if (groundFrames > 3)
                { /// TODO: make an option to apply friction anyway if not after diagonal dash or right after normal dash
                    new_velocity = new Vector2(new_velocity.x * groundFriction, HeroControllerR.current_velocity.y);
                }
            } else 
            { //air friction
                if (allowBunnyHopOption) { groundFrames = 0; }
                else { groundFrames = 3; }
            }
            if (Math.Abs(HeroControllerR.current_velocity.x) < stoppingSpeed)
            {
                new_velocity = new Vector2(0f, HeroControllerR.current_velocity.y);
            }

            //HeroControllerR.rb2d = new Vector2(move_direction * maxRunSpeed, HeroControllerR.current_velocity.y);
            
            if (Math.Sign(new_velocity.x) * Math.Sign(move_direction) >= 0) { // facing speed direction
                float AbsoluteXPV = Math.Abs(move_direction) * maxRunSpeed;
                float AbsoluteXMV = Math.Abs(new_velocity.x);
                new_velocity = new Vector2(Math.Max(AbsoluteXMV, AbsoluteXPV) * Math.Sign(move_direction), HeroControllerR.current_velocity.y);
                //HeroControllerR.rb2d.velocity = new Vector2(Math.Sign(move_direction) * Math.Max(Math.Abs(move_direction) * maxRunSpeed, Math.Abs(HeroControllerR.current_velocity.x)), HeroControllerR.current_velocity.y);
            } else 
            { // resisting movement
                new_velocity = new Vector2(new_velocity.x + move_direction * maxRunSpeed, HeroControllerR.current_velocity.y);
            }
            ///TODO: exceed Run speed only if doing a tech aka allowExceedSpeed
            if (Math.Abs(new_velocity.x) < maxRunSpeed + EPS) { canExceedSpeed = false; }
            if (canExceedSpeed)
            {
                HeroControllerR.rb2d.velocity = new_velocity;
                return;
            }
            if (new_velocity.x < -maxRunSpeed) { new_velocity.x = -maxRunSpeed; }
            if (new_velocity.x > maxRunSpeed) { new_velocity.x = maxRunSpeed; }
            HeroControllerR.rb2d.velocity = new_velocity;
        }

        private bool CanDash8Dir(On.HeroController.orig_CanDash orig, HeroController self)
        {

            if (!orig(self)) { return false; }
            if ((!HeroControllerR.cState.onGround) && (!isDashJumpExtended)) { return false; }
            /// TODO: make sprite flip from hero dash there
            dashFrames = 0;
            //lastDirectionBeforeDash = 
            dashDir.x = BtF(HeroControllerR.inputHandler.inputActions.right.IsPressed)
                - BtF(HeroControllerR.inputHandler.inputActions.left.IsPressed);
            dashDir.y = BtF(HeroControllerR.inputHandler.inputActions.up.IsPressed)
                - BtF(HeroControllerR.inputHandler.inputActions.down.IsPressed);
            dashDir.Normalize();
            /*
            if ((dashDir.x > 0) && (dashDir.y > 0))
            {
                dashDir /= (float)Math.Sqrt(2f);
            }
            */
            if ((Math.Abs(dashDir.x) < 0.001f) && (Math.Abs(dashDir.y) < 0.001f))
            {
                dashDir = new Vector2(BtF(HeroControllerR.cState.facingRight) - BtF(!HeroControllerR.cState.facingRight), 0);
            }
            ///TODO: charm 16
            maxDashSpeed = HeroControllerR.DASH_SPEED;
            
            if (Input.GetKey(NO_SHADOW_DASH_BUTTON) && (HeroControllerR.playerData.GetBool("equippedCharm_31"))) // dashmaster allows use regular dash
            {
                HeroControllerR.shadowDashTimer = 0.001f;
            }
            else if ((HeroControllerR.playerData.GetBool("equippedCharm_16")) && (HeroControllerR.shadowDashTimer < 0.001f))
            {
                maxDashSpeed = HeroControllerR.DASH_SPEED_SHARP;
            }
            float dashSpeedx = Math.Max(maxDashSpeed, Math.Abs(HeroControllerR.current_velocity.x));
            float dashSpeedy = maxDashSpeed;
            if ((dashDir.y < -0.001f)) //&& (maxDashSpeed < Math.Abs(HeroControllerR.current_velocity.x)))
            {
                dashSpeedy = Math.Max(Math.Abs(HeroControllerR.current_velocity.x), maxDashSpeed) * (float)Math.Sqrt(2);
                //dashSpeedy = (HeroControllerR.current_velocity.x) * (float)Math.Sqrt(2);
                dashSpeedx = dashSpeedx * (float)Math.Sqrt(2);
                canExceedSpeed = true;
            }
            dashDir.x *= dashSpeedx;
            dashDir.y *= dashSpeedy;
            return true;
        }

        private bool CanJumpNoDash(On.HeroController.orig_CanJump orig, HeroController self)
        {
            if (!HeroControllerR.acceptingInput) { return false; }
            if (HeroControllerR.cState.wallSliding) { return false; }
            if (HeroControllerR.cState.jumping) { return false; }
            if (HeroControllerR.cState.bouncing) { return false; }
            if (HeroControllerR.cState.shroomBouncing) { return false; }
            if ((HeroControllerR.cState.onGround) ||
               (HeroControllerR.cState.dashing && (Math.Abs(dashDir.y) < EPS) && (IsCloseToGround())) ) {
                Vector2 lastVel = HeroControllerR.rb2d.velocity;
                //HeroControllerR.current_velocity = Vector2(10, 0);
                if ((dashDir.y < -0.001f) && (HeroControllerR.cState.dashing))
                {
                    lastVel.x = maxDashSpeed * Math.Sign(dashDir.x) * 1.2f;
                    lastVel.y = 0f;
                    inHyper = true;
                } else if ((dashDir.y < -0.001f) && (Math.Abs(lastVel.x) > maxDashSpeed - 0.001f)) /// intended ultra
                {
                    lastVel.x = lastVel.x * 1.2f;
                    lastVel.y = 0f;
                    inHyper = true;
                }
                if (Math.Abs(dashDir.y) < EPS)
                {
                    lastVel.x = maxDashSpeed * Math.Sign(dashDir.x);
                }
                if ((HeroControllerR.cState.dashing) && (dashFrames < 8))
                {
                    isDashJumpExtended = false;
                } else if (HeroControllerR.cState.dashing)
                {
                    HeroControllerR.airDashed = false;
                    PlayerDataAccess.canDash = true;
                    HeroControllerR.doubleJumped = false;
                }
                canExceedSpeed = true;
                HeroControllerR.FinishedDashing();
                dashDir = new Vector2(0f, 0f); ///intended ultra
                HeroControllerR.rb2d.velocity = lastVel;
                return true; 
            }

            return orig(self);
            
        }

    }
}

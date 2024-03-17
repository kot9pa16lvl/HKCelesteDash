using Modding;
using System;
using HKMirror;
using HKMirror.Reflection.SingletonClasses;
using UnityEngine;
using UnityEngine.Timeline;
using System.Diagnostics.Eventing.Reader;
using System.Collections.ObjectModel;

namespace CelesteDash
{
    public class CelesteDashMod : Mod
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

            inHyper = false;
            dashDir = Vector2.zero;
            groundFrames = 0;
            dashFrames = 0;
            isDashJumpExtended = true;
            maxDashSpeed = 0;
            recoilSpeed = 20f;
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

        private void CUpdate()
        {
            
            if ((HeroControllerR.cState.recoilingLeft))
            {
                HeroControllerR.rb2d.velocity = new Vector2(-recoilSpeed, HeroControllerR.rb2d.velocity.y);//recoilSpeed;
            }
            if (HeroControllerR.cState.recoilingRight)
            {
                HeroControllerR.rb2d.velocity = new Vector2(recoilSpeed, HeroControllerR.rb2d.velocity.y);
            }
            if (HeroControllerR.cState.onGround && (HeroControllerR.dashCooldownTimer <= 0f))
            {
                isDashJumpExtended = true;
            }
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
            HeroControllerR.rb2d.velocity = new Vector2(HeroControllerR.rb2d.velocity.x, HeroControllerR.JUMP_SPEED);
            HeroControllerR.jump_steps++;
            HeroControllerR.jumped_steps++;
            HeroControllerR.ledgeBufferSteps = 0;
        }

        private void CDash(On.HeroController.orig_Dash orig, HeroController self)
        {
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
            HeroControllerR.rb2d.velocity = dashDir;
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
            orig(self, move_direction); /// temp replace for grounded state
            if (!HeroControllerR.acceptingInput || HeroControllerR.cState.wallSliding) { return; }
            if (HeroControllerR.cState.inWalkZone) {
                HeroControllerR.current_velocity = new Vector2(move_direction * HeroControllerR.WALK_SPEED, HeroControllerR.current_velocity.y);
            }
            ///TODO: charm 31, 37
            float maxRunSpeed = HeroControllerR.RUN_SPEED;
            float stoppingSpeed = 2f;
            float groundFriction = 0.8f;
            Vector2 new_velocity = HeroControllerR.current_velocity;

            if (HeroControllerR.cState.onGround) {// ground friction
                groundFrames += 1;
                if (groundFrames > 3)
                {
                    new_velocity = new Vector2(new_velocity.x * groundFriction, HeroControllerR.current_velocity.y);
                }
            } else 
            { //air friction
                groundFrames = 0;
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
            HeroControllerR.rb2d.velocity = new_velocity;
            
        }

        private bool CanDash8Dir(On.HeroController.orig_CanDash orig, HeroController self)
        {

            if (!orig(self)) { return false; }
            if ((!HeroControllerR.cState.onGround) && (!isDashJumpExtended)) { return false; }
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
            float dashSpeedx = Math.Max(maxDashSpeed, Math.Abs(HeroControllerR.current_velocity.x));
            float dashSpeedy = maxDashSpeed;
            if ((dashDir.y < -0.001f)) //&& (maxDashSpeed < Math.Abs(HeroControllerR.current_velocity.x)))
            {
                dashSpeedy = Math.Max(Math.Abs(HeroControllerR.current_velocity.x), maxDashSpeed) * (float)Math.Sqrt(2);
                //dashSpeedy = (HeroControllerR.current_velocity.x) * (float)Math.Sqrt(2);
                dashSpeedx = dashSpeedx * (float)Math.Sqrt(2);
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
            if (HeroControllerR.cState.onGround) {
                Vector2 lastVel = HeroControllerR.rb2d.velocity;
                //HeroControllerR.current_velocity = Vector2(10, 0);
                if ((dashDir.y < -0.001f) && (HeroControllerR.cState.dashing))
                {
                    lastVel.x = maxDashSpeed * Math.Sign(dashDir.x) * 1.2f;
                    lastVel.y = 0f;
                    inHyper = true;
                } else if (dashDir.y < -0.001f) /// intended ultra
                {
                    lastVel.x = lastVel.x * 1.2f;
                    lastVel.y = 0f;
                    inHyper = true;
                }
                if ((HeroControllerR.cState.dashing) && (dashFrames < 8))
                {
                    isDashJumpExtended = false;
                }
                HeroControllerR.FinishedDashing();
                dashDir = new Vector2(0f, 0f); ///intended ultra
                HeroControllerR.rb2d.velocity = lastVel;
                return true; 
            }
            return orig(self);
            
        }

    }
}

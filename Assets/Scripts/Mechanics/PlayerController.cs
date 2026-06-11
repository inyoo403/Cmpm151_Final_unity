using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Platformer.Audio;
using Platformer.Gameplay;
using static Platformer.Core.Simulation;
using Platformer.Model;
using Platformer.Core;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to allow for any kind of customisation.
    /// </summary>
    public class PlayerController : KinematicObject
    {
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float maxSpeed = 7;

        /// <summary>
        /// Initial jump velocity at the start of a jump.
        /// </summary>
        public float jumpTakeOffSpeed = 7;

        /// <summary>
        /// Duration used to normalize held jump into a 0..1 ratio for external audio.
        /// Higher value means the jump sound takes longer to reach its highest pitch.
        /// </summary>
        public float maxJumpHoldTime = 0.25f;

        public JumpState jumpState = JumpState.Grounded;

        private bool stopJump;
        private bool jumpHeld;
        private float jumpHoldTime;

        /*internal new*/ public Collider2D collider2d;
        /*internal new*/ public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;

        bool jump;
        Vector2 move;
        SpriteRenderer spriteRenderer;
        internal Animator animator;
        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        private InputAction m_MoveAction;
        private InputAction m_JumpAction;

        public Bounds Bounds => collider2d.bounds;

        public float JumpHoldRatio
        {
            get
            {
                if (maxJumpHoldTime <= 0f)
                    return 0f;

                return Mathf.Clamp01(jumpHoldTime / maxJumpHoldTime);
            }
        }

        void Awake()
        {
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");

            m_MoveAction.Enable();
            m_JumpAction.Enable();
        }

        protected override void Update()
        {
            if (controlEnabled)
            {
                move.x = m_MoveAction.ReadValue<Vector2>().x;

                if (jumpState == JumpState.Grounded && m_JumpAction.WasPressedThisFrame())
                {
                    jumpState = JumpState.PrepareToJump;
                    jumpHeld = true;
                    jumpHoldTime = 0f;
                }
                else if (m_JumpAction.WasReleasedThisFrame())
                {
                    stopJump = true;
                    jumpHeld = false;
                    Schedule<PlayerStopJump>().player = this;
                }
            }
            else
            {
                move.x = 0;
                jumpHeld = false;
            }

            UpdateJumpState();
            base.Update();

            UpdateJumpHoldRatio();

            AudioManager.Instance.UpdatePlayerState(this);
        }

        void UpdateJumpHoldRatio()
        {
            bool isRising = velocity.y > 0.01f;

            if (jumpHeld && !IsGrounded && isRising)
            {
                jumpHoldTime += Time.deltaTime;
                jumpHoldTime = Mathf.Min(jumpHoldTime, maxJumpHoldTime);
            }

            if (IsGrounded && jumpState == JumpState.Grounded)
            {
                jumpHoldTime = 0f;
            }
        }

        void UpdateJumpState()
        {
            jump = false;

            switch (jumpState)
            {
                case JumpState.PrepareToJump:
                    jumpState = JumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;

                case JumpState.Jumping:
                    if (!IsGrounded)
                    {
                        Schedule<PlayerJumped>().player = this;
                        jumpState = JumpState.InFlight;
                    }
                    break;

                case JumpState.InFlight:
                    if (IsGrounded)
                    {
                        Schedule<PlayerLanded>().player = this;
                        jumpState = JumpState.Landed;
                    }
                    break;

                case JumpState.Landed:
                    jumpState = JumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (jump && IsGrounded)
            {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                jump = false;
            }
            else if (stopJump)
            {
                stopJump = false;

                if (velocity.y > 0)
                {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            }

            if (move.x > 0.01f)
                spriteRenderer.flipX = false;
            else if (move.x < -0.01f)
                spriteRenderer.flipX = true;

            animator.SetBool("grounded", IsGrounded);
            animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / maxSpeed);

            targetVelocity = move * maxSpeed;
        }

        public enum JumpState
        {
            Grounded,
            PrepareToJump,
            Jumping,
            InFlight,
            Landed
        }
    }
}
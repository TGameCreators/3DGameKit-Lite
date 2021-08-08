using UnityEngine;
using Gamekit3D.Message;
using System.Collections;
using UnityEngine.XR.WSA;

namespace Gamekit3D
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : MonoBehaviour, IMessageReceiver
    {
        protected static PlayerController s_Instance;
        public static PlayerController instance { get { return s_Instance; } }

        public bool respawning { get { return m_Respawning; } }

        public float maxForwardSpeed = 8f;        // How fast Ellen can run.�G�������ǂꂾ����������邩�B
        public float gravity = 20f;               // How fast Ellen accelerates downwards when airborne.�G�������󒆂ŉ������ɉ������鑬���B
        public float jumpSpeed = 10f;             // How fast Ellen takes off when jumping.�G�������W�����v����Ƃ��̗������x�B
        public float minTurnSpeed = 400f;         // How fast Ellen turns when moving at maximum speed.�G�������ő呬�x�œ������Ƃ��̉�]�̑����B
        public float maxTurnSpeed = 1200f;        // How fast Ellen turns when stationary. �G�������Î~���Ă���Ƃ��̉�]�̑���
        public float idleTimeout = 5f;            // How long before Ellen starts considering random idles.�G�����͂ǂ̂��炢�Ń����_���A�C�h�����O���l����悤�ɂȂ�̂��B
        public bool canAttack;                    // Whether or not Ellen can swing her staff.�G�������X�^�b�t��U��񂹂邩�ǂ����B

        public CameraSettings cameraSettings;            // Reference used to determine the camera's direction.�J�����̕��������߂邽�߂̊�B
        public MeleeWeapon meleeWeapon;                  // Reference used to (de)activate the staff when attacking. �U�����ɃX�^�b�t���i�f�B�j�A�N�e�B�u�ɂ��邽�߂̎Q�l�����B
        public RandomAudioPlayer footstepPlayer;         // Random Audio Players used for various situations.�l�X�ȏ�ʂŎg���郉���_���I�[�f�B�I�v���[���[�B
        public RandomAudioPlayer hurtAudioPlayer;
        public RandomAudioPlayer landingPlayer;
        public RandomAudioPlayer emoteLandingPlayer;
        public RandomAudioPlayer emoteDeathPlayer;
        public RandomAudioPlayer emoteAttackPlayer;
        public RandomAudioPlayer emoteJumpPlayer;

        protected AnimatorStateInfo m_CurrentStateInfo;    // Information about the base layer of the animator cached.�A�j���[�^�[�̃x�[�X���C���[�̏�񂪃L���b�V������Ă��܂��B
        protected AnimatorStateInfo m_NextStateInfo;
        protected bool m_IsAnimatorTransitioning;
        protected AnimatorStateInfo m_PreviousCurrentStateInfo;    // Information about the base layer of the animator from last frame.�Ō�̃t���[������̃A�j���[�^�[�̃x�[�X���C���[�̏��B
        protected AnimatorStateInfo m_PreviousNextStateInfo;
        protected bool m_PreviousIsAnimatorTransitioning;
        protected bool m_IsGrounded = true;            // Whether or not Ellen is currently standing on the ground.�G���������ݒn�ʂɗ����Ă��邩�ǂ����B
        protected bool m_PreviouslyGrounded = true;    // Whether or not Ellen was standing on the ground last frame.�Ō�̃t���[���ŃG�������n�ʂɗ����Ă������ǂ����B
        protected bool m_ReadyToJump;                  // Whether or not the input state and Ellen are correct to allow jumping.���͂̏�ԂƃG�������W�����v�������邽�߂ɐ��������ǂ����B
        protected float m_DesiredForwardSpeed;         // How fast Ellen aims be going along the ground based on input.���͂��ꂽ�������ƂɁA�G�������ǂ̂��炢�̑��x�Œn���ڎw���̂��B
        protected float m_ForwardSpeed;                // How fast Ellen is currently going along the ground.���݁A�G�������n�ʂɉ����Ăǂ̂��炢�̑����Ői��ł���̂��B
        protected float m_VerticalSpeed;               // How fast Ellen is currently moving up or down.�G���������݂ǂ̂��炢�̑����ŏ㉺�ɓ����Ă��邩�B
        protected PlayerInput m_Input;                 // Reference used to determine how Ellen should move.�G�������ǂ̂悤�ɓ����ׂ����𔻒f���邽�߂̊�B
        protected CharacterController m_CharCtrl;      // Reference used to actually move Ellen.���ۂɃG�����𓮂����Ă������t�@�����X
        protected Animator m_Animator;                 // Reference used to make decisions based on Ellen's current animation and to set parameters.�G�����̌��݂̃A�j���[�V���������Ĕ��f���A�p�����[�^��ݒ肷�邽�߂̎Q�l�����B
        protected Material m_CurrentWalkingSurface;    // Reference used to make decisions about audio.�I�[�f�B�I�Ɋւ��錈����s�����߂̊�B
        protected Quaternion m_TargetRotation;         // What rotation Ellen is aiming to have based on input.���͂��ꂽ���e�Ɋ�Â��āA�G�������ǂ̂悤�ȉ�]��ڎw���Ă���̂��B
        protected float m_AngleDiff;                   // Angle in degrees between Ellen's current rotation and her target rotation.�G�����̌��݂̉�]�ƖڕW�̉�]�̊Ԃ̊p�x�i�x�j�B
        protected Collider[] m_OverlapResult = new Collider[8];    // Used to cache colliders that are near Ellen.�G�����̋߂��ɂ���R���_�[���L���b�V�����邽�߂Ɏg�p�����B
        protected bool m_InAttack;                     // Whether Ellen is currently in the middle of a melee attack.�G���������݁A�ߐڍU���̍Œ��ł��邩�ǂ����B
        protected bool m_InCombo;                      // Whether Ellen is currently in the middle of her melee combo.�G���������݁A�ߐڃR���{�̍Œ��ł��邩�ǂ����B
        protected Damageable m_Damageable;             // Reference used to set invulnerablity and health based on respawning.���X�|�[�����̖��G�x��w���X��ݒ肷��ۂɎg�p����郊�t�@�����X�ł��B
        protected Renderer[] m_Renderers;              // References used to make sure Renderers are reset properly. �����_�������������Z�b�g����Ă��邱�Ƃ��m�F���邽�߂Ɏg�p����郊�t�@�����X�B
        protected Checkpoint m_CurrentCheckpoint;      // Reference used to reset Ellen to the correct position on respawn.���X�|�[�����ɃG�����𐳂����ʒu�ɖ߂����߂Ɏg�p����郊�t�@�����X�B
        protected bool m_Respawning;                   // Whether Ellen is currently respawning.�G���������݃��X�|�[�����Ă��邩�ǂ����B
        protected float m_IdleTimer;                   // Used to count up to Ellen considering a random idle.�����_���ȃA�C�h�����O���l�����āA�G�����܂ŃJ�E���g�A�b�v���邽�߂Ɏg�p���܂��B

        // These constants are used to ensure Ellen moves and behaves properly.�����̒萔�́AEllen�̓����⓮���K�؂ɂ��邽�߂Ɏg�p����܂��B
        // It is advised you don't change them without fully understanding what they do in code.�R�[�h��̖������\���ɗ��������ɕύX���邱�Ƃ͂����߂��܂���B
        const float k_AirborneTurnSpeedProportion = 5.4f;
        const float k_GroundedRayDistance = 1f;
        const float k_JumpAbortSpeed = 10f;
        const float k_MinEnemyDotCoeff = 0.2f;
        const float k_InverseOneEighty = 1f / 180f;
        const float k_StickingGravityProportion = 0.3f;
        const float k_GroundAcceleration = 20f;
        const float k_GroundDeceleration = 25f;

        // Parameters

        readonly int m_HashAirborneVerticalSpeed = Animator.StringToHash("AirborneVerticalSpeed");
        readonly int m_HashForwardSpeed = Animator.StringToHash("ForwardSpeed");
        readonly int m_HashAngleDeltaRad = Animator.StringToHash("AngleDeltaRad");
        readonly int m_HashTimeoutToIdle = Animator.StringToHash("TimeoutToIdle");
        readonly int m_HashGrounded = Animator.StringToHash("Grounded");
        readonly int m_HashInputDetected = Animator.StringToHash("InputDetected");
        readonly int m_HashMeleeAttack = Animator.StringToHash("MeleeAttack");
        readonly int m_HashHurt = Animator.StringToHash("Hurt");
        readonly int m_HashDeath = Animator.StringToHash("Death");
        readonly int m_HashRespawn = Animator.StringToHash("Respawn");
        readonly int m_HashHurtFromX = Animator.StringToHash("HurtFromX");
        readonly int m_HashHurtFromY = Animator.StringToHash("HurtFromY");
        readonly int m_HashStateTime = Animator.StringToHash("StateTime");
        readonly int m_HashFootFall = Animator.StringToHash("FootFall");

        // States
        readonly int m_HashLocomotion = Animator.StringToHash("Locomotion");
        readonly int m_HashAirborne = Animator.StringToHash("Airborne");
        readonly int m_HashLanding = Animator.StringToHash("Landing");    // Also a parameter.
        readonly int m_HashEllenCombo1 = Animator.StringToHash("EllenCombo1");
        readonly int m_HashEllenCombo2 = Animator.StringToHash("EllenCombo2");
        readonly int m_HashEllenCombo3 = Animator.StringToHash("EllenCombo3");
        readonly int m_HashEllenCombo4 = Animator.StringToHash("EllenCombo4");
        readonly int m_HashEllenDeath = Animator.StringToHash("EllenDeath");

        // Tags
        readonly int m_HashBlockInput = Animator.StringToHash("BlockInput");

        protected bool IsMoveInput
        {
            get { return !Mathf.Approximately(m_Input.MoveInput.sqrMagnitude, 0f); }
        }

        public void SetCanAttack(bool canAttack)
        {
            this.canAttack = canAttack;
        }

        // Called automatically by Unity when the script is first added to a gameobject or is reset from the context menu.
        //�X�N���v�g���Q�[���I�u�W�F�N�g�ɏ��߂Ēǉ����ꂽ�Ƃ��A�܂��̓R���e�L�X�g���j���[���烊�Z�b�g���ꂽ�Ƃ��ɁAUnity�ɂ���Ď����I�ɌĂяo����܂��B
        void Reset()
        {
            meleeWeapon = GetComponentInChildren<MeleeWeapon>();

            Transform footStepSource = transform.Find("FootstepSource");
            if (footStepSource != null)
                footstepPlayer = footStepSource.GetComponent<RandomAudioPlayer>();

            Transform hurtSource = transform.Find("HurtSource");
            if (hurtSource != null)
                hurtAudioPlayer = hurtSource.GetComponent<RandomAudioPlayer>();

            Transform landingSource = transform.Find("LandingSource");
            if (landingSource != null)
                landingPlayer = landingSource.GetComponent<RandomAudioPlayer>();

            cameraSettings = FindObjectOfType<CameraSettings>();

            if (cameraSettings != null)
            {
                if (cameraSettings.follow == null)
                    cameraSettings.follow = transform;

                if (cameraSettings.lookAt == null)
                    cameraSettings.follow = transform.Find("HeadTarget");
            }
        }

        // Called automatically by Unity when the script first exists in the scene.
        //�X�N���v�g���V�[���ɏ��߂đ��݂����Ƃ��ɁAUnity�ɂ���Ď����I�ɌĂяo����܂��B
        void Awake()
        {
            m_Input = GetComponent<PlayerInput>();
            m_Animator = GetComponent<Animator>();
            m_CharCtrl = GetComponent<CharacterController>();

            meleeWeapon.SetOwner(gameObject);

            s_Instance = this;
        }

        // Called automatically by Unity after Awake whenever the script is enabled. 
        //�X�N���v�g���L���ɂȂ��Ă���ƁAAwake���Unity���玩���I�ɌĂяo����܂��B
        void OnEnable()
        {
            SceneLinkedSMB<PlayerController>.Initialise(m_Animator, this);

            m_Damageable = GetComponent<Damageable>();
            m_Damageable.onDamageMessageReceivers.Add(this);

            m_Damageable.isInvulnerable = true;

            EquipMeleeWeapon(false);

            m_Renderers = GetComponentsInChildren<Renderer>();
        }

        // Called automatically by Unity whenever the script is disabled.
        //�X�N���v�g�������ɂȂ�ƁAUnity���玩���I�ɌĂяo����܂��B
        void OnDisable()
        {
            m_Damageable.onDamageMessageReceivers.Remove(this);

            for (int i = 0; i < m_Renderers.Length; ++i)
            {
                m_Renderers[i].enabled = true;
            }
        }

        // Called automatically by Unity once every Physics step.
        //Physics�̃X�e�b�v���ƂɁAUnity���玩���I�ɌĂяo����܂��B
        void FixedUpdate()
        {
            CacheAnimatorState();

            UpdateInputBlocking();

            EquipMeleeWeapon(IsWeaponEquiped());

            m_Animator.SetFloat(m_HashStateTime, Mathf.Repeat(m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f));
            m_Animator.ResetTrigger(m_HashMeleeAttack);

            if (m_Input.Attack && canAttack)
                m_Animator.SetTrigger(m_HashMeleeAttack);

            CalculateForwardMovement();
            CalculateVerticalMovement();

            SetTargetRotation();

            if (IsOrientationUpdated() && IsMoveInput)
                UpdateOrientation();

            PlayAudio();

            TimeoutToIdle();

            m_PreviouslyGrounded = m_IsGrounded;
        }

        /// <summary>
        ///  Called at the start of FixedUpdate to record the current state of the base layer of the animator.
        /// FixedUpdate�̊J�n���ɌĂяo����A�A�j���[�^�[�̃x�[�X���C���[�̌��݂̏�Ԃ��L�^���܂��B
        /// </summary>
        void CacheAnimatorState()
        {
            m_PreviousCurrentStateInfo = m_CurrentStateInfo;
            m_PreviousNextStateInfo = m_NextStateInfo;
            m_PreviousIsAnimatorTransitioning = m_IsAnimatorTransitioning;

            m_CurrentStateInfo = m_Animator.GetCurrentAnimatorStateInfo(0);
            m_NextStateInfo = m_Animator.GetNextAnimatorStateInfo(0);
            m_IsAnimatorTransitioning = m_Animator.IsInTransition(0);
        }

        /// <summary>
        ///  Called after the animator state has been cached to determine whether this script should block user input.
        /// �A�j���[�^�[�̏�Ԃ��L���b�V�����ꂽ��ɌĂяo����A���̃X�N���v�g�����[�U�[�̓��͂��u���b�N���ׂ����ǂ����𔻒f���܂��B
        /// </summary>
        void UpdateInputBlocking()
        {
            bool inputBlocked = m_CurrentStateInfo.tagHash == m_HashBlockInput && !m_IsAnimatorTransitioning;
            inputBlocked |= m_NextStateInfo.tagHash == m_HashBlockInput;
            m_Input.playerControllerInputBlocked = inputBlocked;
        }

        /// <summary>
        /// Called after the animator state has been cached to determine whether or not the staff should be active or not.
        /// �A�j���[�^�[�̏�Ԃ��L���b�V�����ꂽ��ɌĂяo����A�X�^�b�t���A�N�e�B�u�ł���ׂ����ǂ����𔻒f���܂��B
        /// </summary>
        /// <returns></returns>
        bool IsWeaponEquiped()
        {
            bool equipped = m_NextStateInfo.shortNameHash == m_HashEllenCombo1 || m_CurrentStateInfo.shortNameHash == m_HashEllenCombo1;
            equipped |= m_NextStateInfo.shortNameHash == m_HashEllenCombo2 || m_CurrentStateInfo.shortNameHash == m_HashEllenCombo2;
            equipped |= m_NextStateInfo.shortNameHash == m_HashEllenCombo3 || m_CurrentStateInfo.shortNameHash == m_HashEllenCombo3;
            equipped |= m_NextStateInfo.shortNameHash == m_HashEllenCombo4 || m_CurrentStateInfo.shortNameHash == m_HashEllenCombo4;

            return equipped;
        }

        /// <summary>
        ///  Called each physics step with a parameter based on the return value of IsWeaponEquiped.
        ///   �����X�e�b�v���ƂɁAIsWeaponEquiped�̖߂�l�Ɋ�Â����p�����[�^�ŌĂяo����܂��B
        /// </summary>
        /// <param name="equip"></param>
        void EquipMeleeWeapon(bool equip)
        {
            meleeWeapon.gameObject.SetActive(equip);
            m_InAttack = false;
            m_InCombo = equip;

            if (!equip)
                m_Animator.ResetTrigger(m_HashMeleeAttack);
        }

        /// <summary>
        ///  Called each physics step.
        ///   �����w�̃X�e�b�v���ƂɌĂяo�����B
        /// </summary>
        void CalculateForwardMovement()
        {
            // Cache the move input and cap it's magnitude at 1. �ړ����͂��L���b�V�����A���̑傫����1�ɂ��܂��B
            Vector2 moveInput = m_Input.MoveInput;
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            // Calculate the speed intended by input.���͂ňӐ}�������x���v�Z���܂��B
            m_DesiredForwardSpeed = moveInput.magnitude * maxForwardSpeed;

            // Determine change to speed based on whether there is currently any move input.
            // ���݃��[�u���͂����邩�ǂ����ŃX�s�[�h�̕ω��𔻒f���܂��B
            float acceleration = IsMoveInput ? k_GroundAcceleration : k_GroundDeceleration;

            // Adjust the forward speed towards the desired speed.�ړI�̑��x�Ɍ����đO�i���x�𒲐����܂��B
            m_ForwardSpeed = Mathf.MoveTowards(m_ForwardSpeed, m_DesiredForwardSpeed, acceleration * Time.deltaTime);

            // Set the animator parameter to control what animation is being played.
            // animator�p�����[�^��ݒ肵�āA�Đ������A�j���[�V�����𐧌䂵�܂��B
            m_Animator.SetFloat(m_HashForwardSpeed, m_ForwardSpeed);
        }

        /// <summary>
        ///  Calculate Vertical Movement
        ///   ���������̓������v�Z����
        /// </summary>
        void CalculateVerticalMovement()
        {
            // If jump is not currently held and Ellen is on the ground then she is ready to jump.
            //�W�����v���ێ�����Ă��炸�A�G�������n��ɂ���ꍇ�́A�W�����v�̏������ł��Ă��܂��B
            if (!m_Input.JumpInput && m_IsGrounded)
                m_ReadyToJump = true;

            if (m_IsGrounded)
            {
                // When grounded we apply a slight negative vertical speed to make Ellen "stick" to the ground.
                // �ڒn���ɂ́A�G������n�ʂɁu�����v�����邽�߂ɁA�킸���ɕ��̐��������̃X�s�[�h�������܂��B
                m_VerticalSpeed = -gravity * k_StickingGravityProportion;

                // If jump is held, Ellen is ready to jump and not currently in the middle of a melee combo...
                //�W�����v���ێ�����Ă���ꍇ�A�G�����̓W�����v�̏������ł��Ă��āA���݂͋ߐڃR���{�̍Œ��ł͂���܂���...�B
                if (m_Input.JumpInput && m_ReadyToJump && !m_InCombo)
                {
                    // ... then override the previously set vertical speed and make sure she cannot jump again.
                    // ...���̌�A�ȑO�ɐݒ肵�����������̑��x���㏑�����A�ޏ����ĂуW�����v�ł��Ȃ��悤�ɂ��܂��B
                    m_VerticalSpeed = jumpSpeed;
                    m_IsGrounded = false;
                    m_ReadyToJump = false;
                }
            }
            else
            {
                // If Ellen is airborne, the jump button is not held and Ellen is currently moving upwards...
                //�G�������󒆂ɂ���Ƃ��A�W�����v�{�^���������Ă��Ȃ���ԂŁA�G��������Ɍ������Ĉړ����Ă����......�B
                if (!m_Input.JumpInput && m_VerticalSpeed > 0.0f)
                {
                    // ... decrease Ellen's vertical speed.
                    // ...�G�����̐��������̑��x��ቺ������B
                    // This is what causes holding jump to jump higher that tapping jump.
                    //���ꂪ�A�z�[���h�W�����v���^�b�v�W�����v�����������Ԍ����ł��B
                    m_VerticalSpeed -= k_JumpAbortSpeed * Time.deltaTime;
                }

                // If a jump is approximately peaking, make it absolute.
                //�W�����v�����悻�s�[�L���O�ł���΁A��ΓI�Ȃ��̂ɂ��܂��B
                if (Mathf.Approximately(m_VerticalSpeed, 0f))
                {
                    m_VerticalSpeed = 0f;
                }

                // If Ellen is airborne, apply gravity.
                // �G�������󒆂ɂ���ꍇ�́A�d�͂�������B
                m_VerticalSpeed -= gravity * Time.deltaTime;
            }
        }

        /// <summary>
        /// Called each physics step to set the rotation Ellen is aiming to have.
        /// �e�����X�e�b�v���Ăяo���A�G�������ڎw����]��ݒ�B
        /// </summary>
        void SetTargetRotation()
        {
            // Create three variables, move input local to the player, flattened forward direction of the camera and a local target rotation.
            // �v���C���[�̃��[�J���Ȉړ����́A�J�����̃t���b�g�ȑO�i�����A���[�J���ȃ^�[�Q�b�g�̉�]��3�̕ϐ����쐬���܂��B
            Vector2 moveInput = m_Input.MoveInput;
            Vector3 localMovementDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            
            Vector3 forward = Quaternion.Euler(0f, cameraSettings.Current.m_XAxis.Value, 0f) * Vector3.forward;
            forward.y = 0f;
            forward.Normalize();

            Quaternion targetRotation;

            // If the local movement direction is the opposite of forward then the target rotation should be towards the camera.
            //���[�J���̈ړ�������forward�̋t�ł���΁A�^�[�Q�b�g�̉�]�̓J�����Ɍ������čs����ׂ��ł��B
            if (Mathf.Approximately(Vector3.Dot(localMovementDirection, Vector3.forward), -1.0f))
            {
                targetRotation = Quaternion.LookRotation(-forward);
            }
            else
            {
                // Otherwise the rotation should be the offset of the input from the camera's forward.
                //����ȊO�̏ꍇ�́A�J�����̑O������̓��͂̃I�t�Z�b�g����]�Ƃ��܂��B
                Quaternion cameraToInputOffset = Quaternion.FromToRotation(Vector3.forward, localMovementDirection);
                targetRotation = Quaternion.LookRotation(cameraToInputOffset * forward);
            }

            // The desired forward direction of Ellen.
            // �G�����̖]�܂����O�i�����B
            Vector3 resultingForward = targetRotation * Vector3.forward;

            // If attacking try to orient to close enemies.
            //�U������ꍇ�́A�߂��̓G��_���悤�ɂ��܂��傤�B
            if (m_InAttack)
            {
                // Find all the enemies in the local area.
                //���[�J���G���A�ɂ��邷�ׂĂ̓G��������B
                Vector3 centre = transform.position + transform.forward * 2.0f + transform.up;
                Vector3 halfExtents = new Vector3(3.0f, 1.0f, 2.0f);
                int layerMask = 1 << LayerMask.NameToLayer("Enemy");
                int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, m_OverlapResult, targetRotation, layerMask);

                // Go through all the enemies in the local area...
                //���[�J���G���A�̂��ׂĂ̓G���X���[����...�B
                float closestDot = 0.0f;
                Vector3 closestForward = Vector3.zero;
                int closest = -1;

                for (int i = 0; i < count; ++i)
                {
                    // ... and for each get a vector from the player to the enemy.
                    // ...�����āA���ꂼ��Ƀv���C���[����G�ւ̃x�N�g���𓾂�B
                    Vector3 playerToEnemy = m_OverlapResult[i].transform.position - transform.position;
                    playerToEnemy.y = 0;
                    playerToEnemy.Normalize();

                    // Find the dot product between the direction the player wants to go and the direction to the enemy.
                    //�v���C���[�̍s�����������ƁA�G�ւ̕����̃h�b�g�ς����߂܂��B
                    // This will be larger the closer to Ellen's desired direction the direction to the enemy is.
                    //����́A�G�����̊�]��������ɓG�ւ̕������߂��قǑ傫���Ȃ�܂��B
                    float d = Vector3.Dot(resultingForward, playerToEnemy);

                    // Store the closest enemy.�ł��g�߂ȓG���i�[����B
                    if (d > k_MinEnemyDotCoeff && d > closestDot)
                    {
                        closestForward = playerToEnemy;
                        closestDot = d;
                        closest = i;
                    }
                }

                // If there is a close enemy...
                //�߂��G��������...�B
                if (closest != -1)
                {
                    // The desired forward is the direction to the closest enemy.
                    //�ړI�̃t�H���[�h�́A�ł��߂��G�ւ̕����ł��B
                    resultingForward = closestForward;

                    // We also directly set the rotation, as we want snappy fight and orientation isn't updated in the UpdateOrientation function during an atatck.
                    //�܂��A��]�����ڐݒ肵�Ă��܂��B����́A�L���̂��铮�����������邽�߂ŁA�A�^�b �N����UpdateOrientation�֐��ł͕����͍X�V����܂���B
                    transform.rotation = Quaternion.LookRotation(resultingForward);
                }
            }

            // Find the difference between the current rotation of the player and the desired rotation of the player in radians.
            //�v���[���[�̌��݂̉�]�ƁA�v���[���[�̊�]�����]�̍������W�A���ŋ��߂܂��B
            float angleCurrent = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(resultingForward.x, resultingForward.z) * Mathf.Rad2Deg;

            m_AngleDiff = Mathf.DeltaAngle(angleCurrent, targetAngle);
            m_TargetRotation = targetRotation;
        }

        /// <summary>
        ///  Called each physics step to help determine whether Ellen can turn under player input.
        ///  �����w�̊e�X�e�b�v���Ăяo���āA�v���C���[�̓��͂ŃG��������]�ł��邩�ǂ����𔻒f���܂��B
        /// </summary>
        /// <returns></returns>
        bool IsOrientationUpdated()
        {
            bool updateOrientationForLocomotion = !m_IsAnimatorTransitioning && m_CurrentStateInfo.shortNameHash == m_HashLocomotion || m_NextStateInfo.shortNameHash == m_HashLocomotion;
            bool updateOrientationForAirborne = !m_IsAnimatorTransitioning && m_CurrentStateInfo.shortNameHash == m_HashAirborne || m_NextStateInfo.shortNameHash == m_HashAirborne;
            bool updateOrientationForLanding = !m_IsAnimatorTransitioning && m_CurrentStateInfo.shortNameHash == m_HashLanding || m_NextStateInfo.shortNameHash == m_HashLanding;

            return updateOrientationForLocomotion || updateOrientationForAirborne || updateOrientationForLanding || m_InCombo && !m_InAttack;
        }

        /// <summary>
        ///  Called each physics step after SetTargetRotation if there is move input and Ellen is in the correct animator state according to IsOrientationUpdated.
        ///   SetTargetRotation�̌�̊e�����X�e�b�v�ŁA�ړ����͂�����AIsOrientationUpdated�ɏ]���ăG�������������A�j���[�^�[�̏�Ԃɂ���ꍇ�ɌĂяo����܂��B
        /// </summary>
        void UpdateOrientation()
        {
            m_Animator.SetFloat(m_HashAngleDeltaRad, m_AngleDiff * Mathf.Deg2Rad);

            Vector3 localInput = new Vector3(m_Input.MoveInput.x, 0f, m_Input.MoveInput.y);
            float groundedTurnSpeed = Mathf.Lerp(maxTurnSpeed, minTurnSpeed, m_ForwardSpeed / m_DesiredForwardSpeed);
            float actualTurnSpeed = m_IsGrounded ? groundedTurnSpeed : Vector3.Angle(transform.forward, localInput) * k_InverseOneEighty * k_AirborneTurnSpeedProportion * groundedTurnSpeed;
            m_TargetRotation = Quaternion.RotateTowards(transform.rotation, m_TargetRotation, actualTurnSpeed * Time.deltaTime);

            transform.rotation = m_TargetRotation;
        }

        /// <summary>
        ///  Called each physics step to check if audio should be played and if so instruct the relevant random audio player to do so.
        ///   �������Z�̊e�X�e�b�v�ŌĂяo����A�I�[�f�B�I���Đ����ׂ����ǂ������`�F�b�N���A�Đ����ׂ��ꍇ�͊֘A���郉���_���I�[�f�B�I�v���[���[�Ɏw�����܂��B
        /// </summary>
        void PlayAudio()
        {
            float footfallCurve = m_Animator.GetFloat(m_HashFootFall);

            if (footfallCurve > 0.01f && !footstepPlayer.playing && footstepPlayer.canPlay)
            {
                footstepPlayer.playing = true;
                footstepPlayer.canPlay = false;
                footstepPlayer.PlayRandomClip(m_CurrentWalkingSurface, m_ForwardSpeed < 4 ? 0 : 1);
            }
            else if (footstepPlayer.playing)
            {
                footstepPlayer.playing = false;
            }
            else if (footfallCurve < 0.01f && !footstepPlayer.canPlay)
            {
                footstepPlayer.canPlay = true;
            }

            if (m_IsGrounded && !m_PreviouslyGrounded)
            {
                landingPlayer.PlayRandomClip(m_CurrentWalkingSurface, bankId: m_ForwardSpeed < 4 ? 0 : 1);
                emoteLandingPlayer.PlayRandomClip();
            }

            if (!m_IsGrounded && m_PreviouslyGrounded && m_VerticalSpeed > 0f)
            {
                emoteJumpPlayer.PlayRandomClip();
            }

            if (m_CurrentStateInfo.shortNameHash == m_HashHurt && m_PreviousCurrentStateInfo.shortNameHash != m_HashHurt)
            {
                hurtAudioPlayer.PlayRandomClip();
            }

            if (m_CurrentStateInfo.shortNameHash == m_HashEllenDeath && m_PreviousCurrentStateInfo.shortNameHash != m_HashEllenDeath)
            {
                emoteDeathPlayer.PlayRandomClip();
            }

            if (m_CurrentStateInfo.shortNameHash == m_HashEllenCombo1 && m_PreviousCurrentStateInfo.shortNameHash != m_HashEllenCombo1 ||
                m_CurrentStateInfo.shortNameHash == m_HashEllenCombo2 && m_PreviousCurrentStateInfo.shortNameHash != m_HashEllenCombo2 ||
                m_CurrentStateInfo.shortNameHash == m_HashEllenCombo3 && m_PreviousCurrentStateInfo.shortNameHash != m_HashEllenCombo3 ||
                m_CurrentStateInfo.shortNameHash == m_HashEllenCombo4 && m_PreviousCurrentStateInfo.shortNameHash != m_HashEllenCombo4)
            {
                emoteAttackPlayer.PlayRandomClip();
            }
        }

        /// <summary>
        ///  Called each physics step to count up to the point where Ellen considers a random idle.
        ///   �G�����������_���A�C�h�����O���l����Ƃ���܂ŃJ�E���g�A�b�v���邽�߂ɁA�e�����X�e�b�v���Ăяo���܂����B
        /// </summary>
        void TimeoutToIdle()
        {
            bool inputDetected = IsMoveInput || m_Input.Attack || m_Input.JumpInput;
            if (m_IsGrounded && !inputDetected)
            {
                m_IdleTimer += Time.deltaTime;

                if (m_IdleTimer >= idleTimeout)
                {
                    m_IdleTimer = 0f;
                    m_Animator.SetTrigger(m_HashTimeoutToIdle);
                }
            }
            else
            {
                m_IdleTimer = 0f;
                m_Animator.ResetTrigger(m_HashTimeoutToIdle);
            }

            m_Animator.SetBool(m_HashInputDetected, inputDetected);
        }

        /// <summary>
        ///  Called each physics step (so long as the Animator component is set to Animate Physics) after FixedUpdate to override root motion.
        ///   FixedUpdate�̌�A�e�����X�e�b�v�iAnimator�R���|�[�l���g��Animate Physics�ɐݒ肳��Ă���ꍇ�j�ŌĂяo����A���[�g���[�V�������I�[�o�[���C�h���܂��B
        /// </summary>
        void OnAnimatorMove()
        {
            Vector3 movement;

            // If Ellen is on the ground...�G�������n��ɂ�����...�B 
            if (m_IsGrounded)
            {
                // ... raycast into the ground... ......�n�ʂɃ��C�L���X�g����......�B
                RaycastHit hit;
                Ray ray = new Ray(transform.position + Vector3.up * k_GroundedRayDistance * 0.5f, -Vector3.up);
                if (Physics.Raycast(ray, out hit, k_GroundedRayDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    // ... and get the movement of the root motion rotated to lie along the plane of the ground.
                    //...�����āA�����̓�����n�ʂ̕��ʂɉ����ĉ�]�����������𓾂�B
                    movement = Vector3.ProjectOnPlane(m_Animator.deltaPosition, hit.normal);

                    // Also store the current walking surface so the correct audio is played.
                    //�܂��A�������������Đ������悤�ɁA���݂̕��s�ʂ�ۑ����܂��B
                    Renderer groundRenderer = hit.collider.GetComponentInChildren<Renderer>();
                    m_CurrentWalkingSurface = groundRenderer ? groundRenderer.sharedMaterial : null;
                }
                else
                {
                    // If no ground is hit just get the movement as the root motion.
                    //�n�ʂɓ������Ă��Ȃ��ꍇ�́A���̓��������[�g���[�V�����Ƃ��Ď擾���܂��B
                    // Theoretically this should rarely happen as when grounded the ray should always hit.
                    //���_�I�ɂ́A�ڒn���Ă���Ό����͕K��������͂��Ȃ̂ŁA���̂悤�Ȃ��Ƃ͂قƂ�ǋN����Ȃ��͂��ł��B
                    movement = m_Animator.deltaPosition;
                    m_CurrentWalkingSurface = null;
                }
            }
            else
            {
                // If not grounded the movement is just in the forward direction.
                //�ڒn���Ă��Ȃ���΁A�����͂����̑O�i�����ł��B
                movement = m_ForwardSpeed * transform.forward * Time.deltaTime;
            }

            // Rotate the transform of the character controller by the animation's root rotation.
            //�L�����N�^�[�R���g���[���̃g�����X�t�H�[�����A�j���[�V�����̃��[�g���[�e�[�V�����ŉ�]�����܂��B
            m_CharCtrl.transform.rotation *= m_Animator.deltaRotation;

            // Add to the movement with the calculated vertical speed.
            //Add to the movement with the calculated vertical speed.
            movement += m_VerticalSpeed * Vector3.up * Time.deltaTime;

            // Move the character controller.�L�����N�^�[�R���g���[���[�𓮂����B
            m_CharCtrl.Move(movement);

            // After the movement store whether or not the character controller is grounded.
            //�����̌�ɁA�L�����N�^�[�R���g���[���[���ڒn���Ă��邩�ǂ������L�����܂��B
            m_IsGrounded = m_CharCtrl.isGrounded;

            // If Ellen is not on the ground then send the vertical speed to the animator.
            //�G�������n�ʂɂ��Ȃ��ꍇ�́A���������̑��x���A�j���[�^�[�ɑ���܂��B
            // This is so the vertical speed is kept when landing so the correct landing animation is played.
            //����́A�������ɐ��������̑��x���ێ����A�����������A�j���[�V�������Đ����邽�߂ł��B
            if (!m_IsGrounded)
                m_Animator.SetFloat(m_HashAirborneVerticalSpeed, m_VerticalSpeed);

            // Send whether or not Ellen is on the ground to the animator.
            // �G�������n��ɂ��邩�ǂ������A�j���[�^�[�ɑ���B
            m_Animator.SetBool(m_HashGrounded, m_IsGrounded);
        }

        /// <summary>
        /// This is called by an animation event when Ellen swings her staff.
        /// �G���������U��Ƃ��̃A�j���[�V�����C�x���g�ŌĂяo����܂��B
        /// </summary>
        /// <param name="throwing"></param>
        public void MeleeAttackStart(int throwing = 0)
        {
            meleeWeapon.BeginAttack(throwing != 0);
            m_InAttack = true;
        }

        /// <summary>
        ///  This is called by an animation event when Ellen finishes swinging her staff.
        /// ����́A�G���������U��I������Ƃ��ɃA�j���[�V�����C�x���g�ŌĂяo����܂��B
        /// </summary>
        public void MeleeAttackEnd()
        {
            meleeWeapon.EndAttack();
            m_InAttack = false;
        }

        /// <summary>
        ///  This is called by Checkpoints to make sure Ellen respawns correctly.
        /// ����̓G�����̃��X�|�[�����������s���Ă��邩�ǂ������m�F���邽�߂Ƀ`�F�b�N�|�C���g�ŌĂяo����܂��B
        /// </summary>
        /// <param name="checkpoint"></param>
        public void SetCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint != null)
                m_CurrentCheckpoint = checkpoint;
        }

        /// <summary>
        ///  This is usually called by a state machine behaviour on the animator controller but can be called from anywhere.
        /// ����͒ʏ�A�A�j���[�^�[�R���g���[���[�̃X�e�[�g�}�V���r�w�C�r�A����Ăяo����܂����A�ǂ�����ł��Ăяo�����Ƃ��ł��܂��B
        /// </summary>
        public void Respawn()
        {
            StartCoroutine(RespawnRoutine());
        }
        
        protected IEnumerator RespawnRoutine()
        {
            // Wait for the animator to be transitioning from the EllenDeath state.
            //�A�j���[�^�[��EllenDeath�̏�Ԃ���ڍs����̂�҂��܂��B
            while (m_CurrentStateInfo.shortNameHash != m_HashEllenDeath || !m_IsAnimatorTransitioning)
            {
                yield return null;
            }

            // Wait for the screen to fade out. ��ʂ��t�F�[�h�A�E�g����̂�҂��܂��B
            yield return StartCoroutine(ScreenFader.FadeSceneOut());
            while (ScreenFader.IsFading)
            {
                yield return null;
            }

            // Enable spawning.�X�|�[�j���O��L���ɂ���B
            EllenSpawn spawn = GetComponentInChildren<EllenSpawn>();
            spawn.enabled = true;

            // If there is a checkpoint, move Ellen to it.
            // �`�F�b�N�|�C���g������΁A�����ɃG�������ړ�������B
            if (m_CurrentCheckpoint != null)
            {
                transform.position = m_CurrentCheckpoint.transform.position;
                transform.rotation = m_CurrentCheckpoint.transform.rotation;
            }
            else
            {
                Debug.LogError("There is no Checkpoint set, there should always be a checkpoint set. Did you add a checkpoint at the spawn?" +
                    "�F�`�F�b�N�|�C���g���ݒ肳��Ă��܂��񂪁A��Ƀ`�F�b�N�|�C���g���ݒ肳��Ă���͂��ł��B�X�|�[���Ń`�F�b�N�|�C���g��ǉ����܂������H");
            }

            // Set the Respawn parameter of the animator.
            //�A�j���[�^�[��Respawn�p�����[�^��ݒ肵�܂��B
            m_Animator.SetTrigger(m_HashRespawn);

            // Start the respawn graphic effects.
            //���X�|�[���̃O���t�B�b�N���ʂ��J�n���܂��B
            spawn.StartEffect();

            // Wait for the screen to fade in.��ʂ��t�F�[�h�C������̂�҂��܂��B
            // Currently it is not important to yield here but should some changes occur that require waiting until a respawn has finished this will be required.
            //���݂́A�����ł̍~���͏d�v�ł͂���܂��񂪁A���炩�̕ύX�������āA���X�|�[�����I������܂ő҂K�v������ꍇ�ɂ́A���̍~�����K�v�ɂȂ�܂��B
            yield return StartCoroutine(ScreenFader.FadeSceneIn());
            
            m_Damageable.ResetDamage();
        }

        /// <summary>
        ///  Called by a state machine behaviour on Ellen's animator controller.
        ///   �G�����̃A�j���[�^�[�R���g���[���[�̃X�e�[�g�}�V���r�w�C�r�A����Ăяo����܂��B
        /// </summary>
        public void RespawnFinished()
        {
            m_Respawning = false;

            //we set the damageable invincible so we can't get hurt just after being respawned (feel like a double punitive)
            //���X�|�[������Ƀ_���[�W���󂯂Ȃ��悤�ɁA�_���[�W���󂯂₷�����̂𖳓G�ɂ��܂����B
            m_Damageable.isInvulnerable = false;
        }

        /// <summary>
        ///  Called by Ellen's Damageable when she is hurt.
        ///   �G�������������Ƃ��ɁA�G������Damageable�ŌĂ΂��B
        /// </summary>
        /// <param name="type"></param>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        public void OnReceiveMessage(MessageType type, object sender, object data)
        {
            switch (type)
            {
                case MessageType.DAMAGED:
                    {
                        Damageable.DamageMessage damageData = (Damageable.DamageMessage)data;
                        Damaged(damageData);
                    }
                    break;
                case MessageType.DEAD:
                    {
                        Damageable.DamageMessage damageData = (Damageable.DamageMessage)data;
                        Die(damageData);
                    }
                    break;
            }
        }

        /// <summary>
        ///  Called by OnReceiveMessage.
        ///   OnReceiveMessage�ɂ���ČĂяo����܂��B
        /// </summary>
        /// <param name="damageMessage"></param>
        void Damaged(Damageable.DamageMessage damageMessage)
        {
            // Set the Hurt parameter of the animator.
            //�A�j���[�^�[��Hurt�p�����[�^��ݒ肵�܂��B
            m_Animator.SetTrigger(m_HashHurt);

            // Find the direction of the damage.
            //�_���[�W�̕�����������B
            Vector3 forward = damageMessage.damageSource - transform.position;
            forward.y = 0f;

            Vector3 localHurt = transform.InverseTransformDirection(forward);

            // Set the HurtFromX and HurtFromY parameters of the animator based on the direction of the damage.
            //�_���[�W�̕����ɉ����āA�A�j���[�^�[��HurtFromX��HurtFromY�̃p�����[�^��ݒ肵�܂��B
            m_Animator.SetFloat(m_HashHurtFromX, localHurt.x);
            m_Animator.SetFloat(m_HashHurtFromY, localHurt.z);

            // Shake the camera.�J������U��B
            CameraShake.Shake(CameraShake.k_PlayerHitShakeAmount, CameraShake.k_PlayerHitShakeTime);

            // Play an audio clip of being hurt. ����������Ƃ��̉������Đ�����B
            if (hurtAudioPlayer != null)
            {
                hurtAudioPlayer.PlayRandomClip();
            }
        }

        /// <summary>
        ///  Called by OnReceiveMessage and by DeathVolumes in the scene.
        ///   OnReceiveMessage�ƃV�[������DeathVolumes�ɂ���ČĂяo����܂��B
        /// </summary>
        /// <param name="damageMessage"></param>
        public void Die(Damageable.DamageMessage damageMessage)
        {
            m_Animator.SetTrigger(m_HashDeath);
            m_ForwardSpeed = 0f;
            m_VerticalSpeed = 0f;
            m_Respawning = true;
            m_Damageable.isInvulnerable = true;
        }
    }
}
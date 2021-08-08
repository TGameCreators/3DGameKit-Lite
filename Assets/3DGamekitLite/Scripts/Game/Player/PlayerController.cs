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

        public float maxForwardSpeed = 8f;        // How fast Ellen can run.エレンがどれだけ速く走れるか。
        public float gravity = 20f;               // How fast Ellen accelerates downwards when airborne.エレンが空中で下方向に加速する速さ。
        public float jumpSpeed = 10f;             // How fast Ellen takes off when jumping.エレンがジャンプするときの離陸速度。
        public float minTurnSpeed = 400f;         // How fast Ellen turns when moving at maximum speed.エレンが最大速度で動いたときの回転の速さ。
        public float maxTurnSpeed = 1200f;        // How fast Ellen turns when stationary. エレンが静止しているときの回転の速さ
        public float idleTimeout = 5f;            // How long before Ellen starts considering random idles.エレンはどのくらいでランダムアイドリングを考えるようになるのか。
        public bool canAttack;                    // Whether or not Ellen can swing her staff.エレンがスタッフを振り回せるかどうか。

        public CameraSettings cameraSettings;            // Reference used to determine the camera's direction.カメラの方向を決めるための基準。
        public MeleeWeapon meleeWeapon;                  // Reference used to (de)activate the staff when attacking. 攻撃時にスタッフを（ディ）アクティブにするための参考資料。
        public RandomAudioPlayer footstepPlayer;         // Random Audio Players used for various situations.様々な場面で使われるランダムオーディオプレーヤー。
        public RandomAudioPlayer hurtAudioPlayer;
        public RandomAudioPlayer landingPlayer;
        public RandomAudioPlayer emoteLandingPlayer;
        public RandomAudioPlayer emoteDeathPlayer;
        public RandomAudioPlayer emoteAttackPlayer;
        public RandomAudioPlayer emoteJumpPlayer;

        protected AnimatorStateInfo m_CurrentStateInfo;    // Information about the base layer of the animator cached.アニメーターのベースレイヤーの情報がキャッシュされています。
        protected AnimatorStateInfo m_NextStateInfo;
        protected bool m_IsAnimatorTransitioning;
        protected AnimatorStateInfo m_PreviousCurrentStateInfo;    // Information about the base layer of the animator from last frame.最後のフレームからのアニメーターのベースレイヤーの情報。
        protected AnimatorStateInfo m_PreviousNextStateInfo;
        protected bool m_PreviousIsAnimatorTransitioning;
        protected bool m_IsGrounded = true;            // Whether or not Ellen is currently standing on the ground.エレンが現在地面に立っているかどうか。
        protected bool m_PreviouslyGrounded = true;    // Whether or not Ellen was standing on the ground last frame.最後のフレームでエレンが地面に立っていたかどうか。
        protected bool m_ReadyToJump;                  // Whether or not the input state and Ellen are correct to allow jumping.入力の状態とエレンがジャンプを許可するために正しいかどうか。
        protected float m_DesiredForwardSpeed;         // How fast Ellen aims be going along the ground based on input.入力された情報をもとに、エレンがどのくらいの速度で地上を目指すのか。
        protected float m_ForwardSpeed;                // How fast Ellen is currently going along the ground.現在、エレンが地面に沿ってどのくらいの速さで進んでいるのか。
        protected float m_VerticalSpeed;               // How fast Ellen is currently moving up or down.エレンが現在どのくらいの速さで上下に動いているか。
        protected PlayerInput m_Input;                 // Reference used to determine how Ellen should move.エレンがどのように動くべきかを判断するための基準。
        protected CharacterController m_CharCtrl;      // Reference used to actually move Ellen.実際にエレンを動かしていたリファレンス
        protected Animator m_Animator;                 // Reference used to make decisions based on Ellen's current animation and to set parameters.エレンの現在のアニメーションを見て判断し、パラメータを設定するための参考資料。
        protected Material m_CurrentWalkingSurface;    // Reference used to make decisions about audio.オーディオに関する決定を行うための基準。
        protected Quaternion m_TargetRotation;         // What rotation Ellen is aiming to have based on input.入力された内容に基づいて、エレンがどのような回転を目指しているのか。
        protected float m_AngleDiff;                   // Angle in degrees between Ellen's current rotation and her target rotation.エレンの現在の回転と目標の回転の間の角度（度）。
        protected Collider[] m_OverlapResult = new Collider[8];    // Used to cache colliders that are near Ellen.エレンの近くにあるコリダーをキャッシュするために使用される。
        protected bool m_InAttack;                     // Whether Ellen is currently in the middle of a melee attack.エレンが現在、近接攻撃の最中であるかどうか。
        protected bool m_InCombo;                      // Whether Ellen is currently in the middle of her melee combo.エレンが現在、近接コンボの最中であるかどうか。
        protected Damageable m_Damageable;             // Reference used to set invulnerablity and health based on respawning.リスポーン時の無敵度やヘルスを設定する際に使用されるリファレンスです。
        protected Renderer[] m_Renderers;              // References used to make sure Renderers are reset properly. レンダラが正しくリセットされていることを確認するために使用されるリファレンス。
        protected Checkpoint m_CurrentCheckpoint;      // Reference used to reset Ellen to the correct position on respawn.リスポーン時にエレンを正しい位置に戻すために使用されるリファレンス。
        protected bool m_Respawning;                   // Whether Ellen is currently respawning.エレンが現在リスポーンしているかどうか。
        protected float m_IdleTimer;                   // Used to count up to Ellen considering a random idle.ランダムなアイドリングを考慮して、エレンまでカウントアップするために使用します。

        // These constants are used to ensure Ellen moves and behaves properly.これらの定数は、Ellenの動きや動作を適切にするために使用されます。
        // It is advised you don't change them without fully understanding what they do in code.コード上の役割を十分に理解せずに変更することはお勧めしません。
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
        //スクリプトがゲームオブジェクトに初めて追加されたとき、またはコンテキストメニューからリセットされたときに、Unityによって自動的に呼び出されます。
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
        //スクリプトがシーンに初めて存在したときに、Unityによって自動的に呼び出されます。
        void Awake()
        {
            m_Input = GetComponent<PlayerInput>();
            m_Animator = GetComponent<Animator>();
            m_CharCtrl = GetComponent<CharacterController>();

            meleeWeapon.SetOwner(gameObject);

            s_Instance = this;
        }

        // Called automatically by Unity after Awake whenever the script is enabled. 
        //スクリプトが有効になっていると、Awake後にUnityから自動的に呼び出されます。
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
        //スクリプトが無効になると、Unityから自動的に呼び出されます。
        void OnDisable()
        {
            m_Damageable.onDamageMessageReceivers.Remove(this);

            for (int i = 0; i < m_Renderers.Length; ++i)
            {
                m_Renderers[i].enabled = true;
            }
        }

        // Called automatically by Unity once every Physics step.
        //Physicsのステップごとに、Unityから自動的に呼び出されます。
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
        /// FixedUpdateの開始時に呼び出され、アニメーターのベースレイヤーの現在の状態を記録します。
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
        /// アニメーターの状態がキャッシュされた後に呼び出され、このスクリプトがユーザーの入力をブロックすべきかどうかを判断します。
        /// </summary>
        void UpdateInputBlocking()
        {
            bool inputBlocked = m_CurrentStateInfo.tagHash == m_HashBlockInput && !m_IsAnimatorTransitioning;
            inputBlocked |= m_NextStateInfo.tagHash == m_HashBlockInput;
            m_Input.playerControllerInputBlocked = inputBlocked;
        }

        /// <summary>
        /// Called after the animator state has been cached to determine whether or not the staff should be active or not.
        /// アニメーターの状態がキャッシュされた後に呼び出され、スタッフがアクティブであるべきかどうかを判断します。
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
        ///   物理ステップごとに、IsWeaponEquipedの戻り値に基づいたパラメータで呼び出されます。
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
        ///   物理学のステップごとに呼び出される。
        /// </summary>
        void CalculateForwardMovement()
        {
            // Cache the move input and cap it's magnitude at 1. 移動入力をキャッシュし、その大きさを1にします。
            Vector2 moveInput = m_Input.MoveInput;
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            // Calculate the speed intended by input.入力で意図した速度を計算します。
            m_DesiredForwardSpeed = moveInput.magnitude * maxForwardSpeed;

            // Determine change to speed based on whether there is currently any move input.
            // 現在ムーブ入力があるかどうかでスピードの変化を判断します。
            float acceleration = IsMoveInput ? k_GroundAcceleration : k_GroundDeceleration;

            // Adjust the forward speed towards the desired speed.目的の速度に向けて前進速度を調整します。
            m_ForwardSpeed = Mathf.MoveTowards(m_ForwardSpeed, m_DesiredForwardSpeed, acceleration * Time.deltaTime);

            // Set the animator parameter to control what animation is being played.
            // animatorパラメータを設定して、再生されるアニメーションを制御します。
            m_Animator.SetFloat(m_HashForwardSpeed, m_ForwardSpeed);
        }

        /// <summary>
        ///  Calculate Vertical Movement
        ///   垂直方向の動きを計算する
        /// </summary>
        void CalculateVerticalMovement()
        {
            // If jump is not currently held and Ellen is on the ground then she is ready to jump.
            //ジャンプが保持されておらず、エレンが地上にいる場合は、ジャンプの準備ができています。
            if (!m_Input.JumpInput && m_IsGrounded)
                m_ReadyToJump = true;

            if (m_IsGrounded)
            {
                // When grounded we apply a slight negative vertical speed to make Ellen "stick" to the ground.
                // 接地時には、エレンを地面に「密着」させるために、わずかに負の垂直方向のスピードをかけます。
                m_VerticalSpeed = -gravity * k_StickingGravityProportion;

                // If jump is held, Ellen is ready to jump and not currently in the middle of a melee combo...
                //ジャンプが保持されている場合、エレンはジャンプの準備ができていて、現在は近接コンボの最中ではありません...。
                if (m_Input.JumpInput && m_ReadyToJump && !m_InCombo)
                {
                    // ... then override the previously set vertical speed and make sure she cannot jump again.
                    // ...その後、以前に設定した垂直方向の速度を上書きし、彼女が再びジャンプできないようにします。
                    m_VerticalSpeed = jumpSpeed;
                    m_IsGrounded = false;
                    m_ReadyToJump = false;
                }
            }
            else
            {
                // If Ellen is airborne, the jump button is not held and Ellen is currently moving upwards...
                //エレンが空中にいるとき、ジャンプボタンを押していない状態で、エレンが上に向かって移動していると......。
                if (!m_Input.JumpInput && m_VerticalSpeed > 0.0f)
                {
                    // ... decrease Ellen's vertical speed.
                    // ...エレンの垂直方向の速度を低下させる。
                    // This is what causes holding jump to jump higher that tapping jump.
                    //これが、ホールドジャンプがタップジャンプよりも高く跳ぶ原因です。
                    m_VerticalSpeed -= k_JumpAbortSpeed * Time.deltaTime;
                }

                // If a jump is approximately peaking, make it absolute.
                //ジャンプがおよそピーキングであれば、絶対的なものにします。
                if (Mathf.Approximately(m_VerticalSpeed, 0f))
                {
                    m_VerticalSpeed = 0f;
                }

                // If Ellen is airborne, apply gravity.
                // エレンが空中にいる場合は、重力をかける。
                m_VerticalSpeed -= gravity * Time.deltaTime;
            }
        }

        /// <summary>
        /// Called each physics step to set the rotation Ellen is aiming to have.
        /// 各物理ステップを呼び出し、エレンが目指す回転を設定。
        /// </summary>
        void SetTargetRotation()
        {
            // Create three variables, move input local to the player, flattened forward direction of the camera and a local target rotation.
            // プレイヤーのローカルな移動入力、カメラのフラットな前進方向、ローカルなターゲットの回転の3つの変数を作成します。
            Vector2 moveInput = m_Input.MoveInput;
            Vector3 localMovementDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            
            Vector3 forward = Quaternion.Euler(0f, cameraSettings.Current.m_XAxis.Value, 0f) * Vector3.forward;
            forward.y = 0f;
            forward.Normalize();

            Quaternion targetRotation;

            // If the local movement direction is the opposite of forward then the target rotation should be towards the camera.
            //ローカルの移動方向がforwardの逆であれば、ターゲットの回転はカメラに向かって行われるべきです。
            if (Mathf.Approximately(Vector3.Dot(localMovementDirection, Vector3.forward), -1.0f))
            {
                targetRotation = Quaternion.LookRotation(-forward);
            }
            else
            {
                // Otherwise the rotation should be the offset of the input from the camera's forward.
                //それ以外の場合は、カメラの前方からの入力のオフセットを回転とします。
                Quaternion cameraToInputOffset = Quaternion.FromToRotation(Vector3.forward, localMovementDirection);
                targetRotation = Quaternion.LookRotation(cameraToInputOffset * forward);
            }

            // The desired forward direction of Ellen.
            // エレンの望ましい前進方向。
            Vector3 resultingForward = targetRotation * Vector3.forward;

            // If attacking try to orient to close enemies.
            //攻撃する場合は、近くの敵を狙うようにしましょう。
            if (m_InAttack)
            {
                // Find all the enemies in the local area.
                //ローカルエリアにいるすべての敵を見つける。
                Vector3 centre = transform.position + transform.forward * 2.0f + transform.up;
                Vector3 halfExtents = new Vector3(3.0f, 1.0f, 2.0f);
                int layerMask = 1 << LayerMask.NameToLayer("Enemy");
                int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, m_OverlapResult, targetRotation, layerMask);

                // Go through all the enemies in the local area...
                //ローカルエリアのすべての敵をスルーして...。
                float closestDot = 0.0f;
                Vector3 closestForward = Vector3.zero;
                int closest = -1;

                for (int i = 0; i < count; ++i)
                {
                    // ... and for each get a vector from the player to the enemy.
                    // ...そして、それぞれにプレイヤーから敵へのベクトルを得る。
                    Vector3 playerToEnemy = m_OverlapResult[i].transform.position - transform.position;
                    playerToEnemy.y = 0;
                    playerToEnemy.Normalize();

                    // Find the dot product between the direction the player wants to go and the direction to the enemy.
                    //プレイヤーの行きたい方向と、敵への方向のドット積を求めます。
                    // This will be larger the closer to Ellen's desired direction the direction to the enemy is.
                    //これは、エレンの希望する方向に敵への方向が近いほど大きくなります。
                    float d = Vector3.Dot(resultingForward, playerToEnemy);

                    // Store the closest enemy.最も身近な敵を格納する。
                    if (d > k_MinEnemyDotCoeff && d > closestDot)
                    {
                        closestForward = playerToEnemy;
                        closestDot = d;
                        closest = i;
                    }
                }

                // If there is a close enemy...
                //近い敵がいたら...。
                if (closest != -1)
                {
                    // The desired forward is the direction to the closest enemy.
                    //目的のフォワードは、最も近い敵への方向です。
                    resultingForward = closestForward;

                    // We also directly set the rotation, as we want snappy fight and orientation isn't updated in the UpdateOrientation function during an atatck.
                    //また、回転も直接設定しています。これは、キレのある動きを実現するためで、アタッ ク中のUpdateOrientation関数では方向は更新されません。
                    transform.rotation = Quaternion.LookRotation(resultingForward);
                }
            }

            // Find the difference between the current rotation of the player and the desired rotation of the player in radians.
            //プレーヤーの現在の回転と、プレーヤーの希望する回転の差をラジアンで求めます。
            float angleCurrent = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(resultingForward.x, resultingForward.z) * Mathf.Rad2Deg;

            m_AngleDiff = Mathf.DeltaAngle(angleCurrent, targetAngle);
            m_TargetRotation = targetRotation;
        }

        /// <summary>
        ///  Called each physics step to help determine whether Ellen can turn under player input.
        ///  物理学の各ステップを呼び出して、プレイヤーの入力でエレンが回転できるかどうかを判断します。
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
        ///   SetTargetRotationの後の各物理ステップで、移動入力があり、IsOrientationUpdatedに従ってエレンが正しいアニメーターの状態にある場合に呼び出されます。
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
        ///   物理演算の各ステップで呼び出され、オーディオを再生すべきかどうかをチェックし、再生すべき場合は関連するランダムオーディオプレーヤーに指示します。
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
        ///   エレンがランダムアイドリングを考えるところまでカウントアップするために、各物理ステップを呼び出しました。
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
        ///   FixedUpdateの後、各物理ステップ（AnimatorコンポーネントがAnimate Physicsに設定されている場合）で呼び出され、ルートモーションをオーバーライドします。
        /// </summary>
        void OnAnimatorMove()
        {
            Vector3 movement;

            // If Ellen is on the ground...エレンが地上にいたら...。 
            if (m_IsGrounded)
            {
                // ... raycast into the ground... ......地面にレイキャストする......。
                RaycastHit hit;
                Ray ray = new Ray(transform.position + Vector3.up * k_GroundedRayDistance * 0.5f, -Vector3.up);
                if (Physics.Raycast(ray, out hit, k_GroundedRayDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    // ... and get the movement of the root motion rotated to lie along the plane of the ground.
                    //...そして、根元の動きを地面の平面に沿って回転させた動きを得る。
                    movement = Vector3.ProjectOnPlane(m_Animator.deltaPosition, hit.normal);

                    // Also store the current walking surface so the correct audio is played.
                    //また、正しい音声が再生されるように、現在の歩行面を保存します。
                    Renderer groundRenderer = hit.collider.GetComponentInChildren<Renderer>();
                    m_CurrentWalkingSurface = groundRenderer ? groundRenderer.sharedMaterial : null;
                }
                else
                {
                    // If no ground is hit just get the movement as the root motion.
                    //地面に当たっていない場合は、その動きをルートモーションとして取得します。
                    // Theoretically this should rarely happen as when grounded the ray should always hit.
                    //理論的には、接地していれば光線は必ず当たるはずなので、このようなことはほとんど起こらないはずです。
                    movement = m_Animator.deltaPosition;
                    m_CurrentWalkingSurface = null;
                }
            }
            else
            {
                // If not grounded the movement is just in the forward direction.
                //接地していなければ、動きはただの前進方向です。
                movement = m_ForwardSpeed * transform.forward * Time.deltaTime;
            }

            // Rotate the transform of the character controller by the animation's root rotation.
            //キャラクターコントローラのトランスフォームをアニメーションのルートローテーションで回転させます。
            m_CharCtrl.transform.rotation *= m_Animator.deltaRotation;

            // Add to the movement with the calculated vertical speed.
            //Add to the movement with the calculated vertical speed.
            movement += m_VerticalSpeed * Vector3.up * Time.deltaTime;

            // Move the character controller.キャラクターコントローラーを動かす。
            m_CharCtrl.Move(movement);

            // After the movement store whether or not the character controller is grounded.
            //動きの後に、キャラクターコントローラーが接地しているかどうかを記憶します。
            m_IsGrounded = m_CharCtrl.isGrounded;

            // If Ellen is not on the ground then send the vertical speed to the animator.
            //エレンが地面にいない場合は、垂直方向の速度をアニメーターに送ります。
            // This is so the vertical speed is kept when landing so the correct landing animation is played.
            //これは、着陸時に垂直方向の速度を維持し、正しい着陸アニメーションを再生するためです。
            if (!m_IsGrounded)
                m_Animator.SetFloat(m_HashAirborneVerticalSpeed, m_VerticalSpeed);

            // Send whether or not Ellen is on the ground to the animator.
            // エレンが地上にいるかどうかをアニメーターに送る。
            m_Animator.SetBool(m_HashGrounded, m_IsGrounded);
        }

        /// <summary>
        /// This is called by an animation event when Ellen swings her staff.
        /// エレンが杖を振るときのアニメーションイベントで呼び出されます。
        /// </summary>
        /// <param name="throwing"></param>
        public void MeleeAttackStart(int throwing = 0)
        {
            meleeWeapon.BeginAttack(throwing != 0);
            m_InAttack = true;
        }

        /// <summary>
        ///  This is called by an animation event when Ellen finishes swinging her staff.
        /// これは、エレンが杖を振り終わったときにアニメーションイベントで呼び出されます。
        /// </summary>
        public void MeleeAttackEnd()
        {
            meleeWeapon.EndAttack();
            m_InAttack = false;
        }

        /// <summary>
        ///  This is called by Checkpoints to make sure Ellen respawns correctly.
        /// これはエレンのリスポーンが正しく行われているかどうかを確認するためにチェックポイントで呼び出されます。
        /// </summary>
        /// <param name="checkpoint"></param>
        public void SetCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint != null)
                m_CurrentCheckpoint = checkpoint;
        }

        /// <summary>
        ///  This is usually called by a state machine behaviour on the animator controller but can be called from anywhere.
        /// これは通常、アニメーターコントローラーのステートマシンビヘイビアから呼び出されますが、どこからでも呼び出すことができます。
        /// </summary>
        public void Respawn()
        {
            StartCoroutine(RespawnRoutine());
        }
        
        protected IEnumerator RespawnRoutine()
        {
            // Wait for the animator to be transitioning from the EllenDeath state.
            //アニメーターがEllenDeathの状態から移行するのを待ちます。
            while (m_CurrentStateInfo.shortNameHash != m_HashEllenDeath || !m_IsAnimatorTransitioning)
            {
                yield return null;
            }

            // Wait for the screen to fade out. 画面がフェードアウトするのを待ちます。
            yield return StartCoroutine(ScreenFader.FadeSceneOut());
            while (ScreenFader.IsFading)
            {
                yield return null;
            }

            // Enable spawning.スポーニングを有効にする。
            EllenSpawn spawn = GetComponentInChildren<EllenSpawn>();
            spawn.enabled = true;

            // If there is a checkpoint, move Ellen to it.
            // チェックポイントがあれば、そこにエレンを移動させる。
            if (m_CurrentCheckpoint != null)
            {
                transform.position = m_CurrentCheckpoint.transform.position;
                transform.rotation = m_CurrentCheckpoint.transform.rotation;
            }
            else
            {
                Debug.LogError("There is no Checkpoint set, there should always be a checkpoint set. Did you add a checkpoint at the spawn?" +
                    "：チェックポイントが設定されていませんが、常にチェックポイントが設定されているはずです。スポーンでチェックポイントを追加しましたか？");
            }

            // Set the Respawn parameter of the animator.
            //アニメーターのRespawnパラメータを設定します。
            m_Animator.SetTrigger(m_HashRespawn);

            // Start the respawn graphic effects.
            //リスポーンのグラフィック効果を開始します。
            spawn.StartEffect();

            // Wait for the screen to fade in.画面がフェードインするのを待ちます。
            // Currently it is not important to yield here but should some changes occur that require waiting until a respawn has finished this will be required.
            //現在は、ここでの降伏は重要ではありませんが、何らかの変更があって、リスポーンが終了するまで待つ必要がある場合には、この降伏が必要になります。
            yield return StartCoroutine(ScreenFader.FadeSceneIn());
            
            m_Damageable.ResetDamage();
        }

        /// <summary>
        ///  Called by a state machine behaviour on Ellen's animator controller.
        ///   エレンのアニメーターコントローラーのステートマシンビヘイビアから呼び出されます。
        /// </summary>
        public void RespawnFinished()
        {
            m_Respawning = false;

            //we set the damageable invincible so we can't get hurt just after being respawned (feel like a double punitive)
            //リスポーン直後にダメージを受けないように、ダメージを受けやすいものを無敵にしました。
            m_Damageable.isInvulnerable = false;
        }

        /// <summary>
        ///  Called by Ellen's Damageable when she is hurt.
        ///   エレンが傷ついたときに、エレンのDamageableで呼ばれる。
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
        ///   OnReceiveMessageによって呼び出されます。
        /// </summary>
        /// <param name="damageMessage"></param>
        void Damaged(Damageable.DamageMessage damageMessage)
        {
            // Set the Hurt parameter of the animator.
            //アニメーターのHurtパラメータを設定します。
            m_Animator.SetTrigger(m_HashHurt);

            // Find the direction of the damage.
            //ダメージの方向を見つける。
            Vector3 forward = damageMessage.damageSource - transform.position;
            forward.y = 0f;

            Vector3 localHurt = transform.InverseTransformDirection(forward);

            // Set the HurtFromX and HurtFromY parameters of the animator based on the direction of the damage.
            //ダメージの方向に応じて、アニメーターのHurtFromXとHurtFromYのパラメータを設定します。
            m_Animator.SetFloat(m_HashHurtFromX, localHurt.x);
            m_Animator.SetFloat(m_HashHurtFromY, localHurt.z);

            // Shake the camera.カメラを振る。
            CameraShake.Shake(CameraShake.k_PlayerHitShakeAmount, CameraShake.k_PlayerHitShakeTime);

            // Play an audio clip of being hurt. 怪我をしたときの音声を再生する。
            if (hurtAudioPlayer != null)
            {
                hurtAudioPlayer.PlayRandomClip();
            }
        }

        /// <summary>
        ///  Called by OnReceiveMessage and by DeathVolumes in the scene.
        ///   OnReceiveMessageとシーン内のDeathVolumesによって呼び出されます。
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
# Task_Supercent

하이브리드 캐주얼 루프 게임 — 슈퍼센트 과제전형 프로젝트  
Unity 2022.3.62f2 · URP · Android / PC · 개인 개발 · 2026.04

## 소개

플레이어가 광산에서 Ore를 채굴하면 CuffFactory → Worker → DeskFacility → Prisoner → JailFacility 순서로 자원이 흘러가는 방치형 루프 게임.  
진행은 ScriptableObject에 선언적으로 정의된 Stage Flow 규칙에 따라 Zone 이벤트가 발화될 때마다 NPC 고용·장비 업그레이드·감옥 확장이 단계적으로 열린다.

## 기술 스택

| 분류 | 내용 |
|---|---|
| 엔진 | Unity 2022.3.62f2 · URP |
| 언어 | C# |
| 입력 | New Input System + FloatingJoystick 병행 (`activeInputHandler: 2`) |
| UI | UGUI · TextMeshPro · 커스텀 TMP 외곽선 컴포넌트 |
| 렌더링 | URP 3단계 품질 (Performant / Balanced / High Fidelity) |
| 빌드 대상 | Android (Balanced) · PC (High Fidelity) |

---

## 핵심 기능

### 1. ScriptableObject 기반 Stage Flow

Zone 전이 규칙을 코드가 아닌 `InteractionZoneFlowLibrary` SO에 선언적으로 정의해, 기획 변경 시 코드 수정 없이 에디터에서 흐름을 조정할 수 있다.

```
InteractionZoneFlowLibrary (SO)
  ├── InitialStates       : 각 Zone의 초기 활성/비활성 상태
  ├── Transitions         : 트리거(OnFirstInteraction / OnCompleted / OnJailBecameFull)
  │                          → Target Zone에 SetZoneEnabled / SetGameObjectActive /
  │                             SetCompleted / ChangeType 적용
  └── PurchaseUpgrades    : Source Zone 완료 → Target Zone 구매 단계 재구성
```

`GameManager.Start()`에서 `StageProgressManager.Initialize()`를 호출하면 세 서브시스템이 초기화된다.

| 서브시스템 | 클래스 | 역할 |
|---|---|---|
| Zone 흐름 | `ZoneFlowController` | 씬 초기 상태 적용, 전이 실행 |
| 구매 업그레이드 | `ZonePurchaseUpgradeService` | Zone 완료 → 장비 재설정 |
| 외부 상태 감시 | `StageStateMonitor` | JailFacility.StateChanged → Jail Flow 전이 |

Zone 완료 전이는 `HashSet<InteractionZoneId>` 가드(`_appliedCompletedTransitionSourceIds`)로 중복 실행을 방지한다.

**씬 등록 진행 흐름:**

1. `CollectMoney` 첫 상호작용 → `BuyEquip` 활성
2. `BuyEquip` 완료 → `BuyMiner` 활성 + `ZonePurchaseUpgradeService`가 BuyEquip을 불도저(price=50)로 재구성
3. `BuyMiner` 완료 → `BuyWorker` 활성
4. `OnJailBecameFull` → `BuyJail` 활성

관련 소스
- `Assets/_Scripts/System/GameManager.cs`
- `Assets/_Scripts/System/Stage/StageProgressManager.cs`
- `Assets/_Scripts/System/Stage/ZoneFlowController.cs`
- `Assets/_Scripts/System/Stage/ZonePurchaseUpgradeService.cs`
- `Assets/_Scripts/System/Stage/StageStateMonitor.cs`
- `Assets/_Scripts/System/Stage/ZoneRegistry.cs`

---

### 2. 이벤트 드리븐 InteractionZone

Zone은 `Started / Completed / StateChanged` 세 이벤트를 발행하기만 하고, 구독자가 필요한 처리를 담당한다. Zone은 StageProgressManager나 MinerManager의 존재를 전혀 모른다.

```
InteractionZone (발행자)
  ├── Started      → StageProgressManager.OnZoneStarted (Flow 전이)
  │                → StageCameraDirectorTrigger (카메라 연출)
  │                → StageGuideIndicatorUI (가이드 화살표 전환)
  ├── Completed    → StageProgressManager.OnZoneCompleted (업그레이드·후처리)
  │                → MinerManager / WorkerManager (NPC 스폰 트리거)
  └── StateChanged → BuyZoneWorldUIBinder (진행 바·아이콘·수량 텍스트 갱신)
```

처리 정책은 정적 클래스 `InteractionZoneActionController`가 담당한다. Zone 타입 5종(BuyEquip · Submit · Collect · BuyNpc · ExpandJail)의 tick 간격·처리량·완료 조건·buy 액션 판단이 이 클래스에 집중돼, Zone 자체는 상태 보유만 한다(SRP).

`IInteractionActor` 인터페이스로 상호작용 주체를 추상화해 Zone 코드 변경 없이 새 Actor 추가가 가능하다(OCP). `InteractionActorResolver`가 Collider → IInteractionActor 변환 책임을 분리한다.

관련 소스
- `Assets/_Scripts/Interaction/InteractionZone.cs`
- `Assets/_Scripts/Interaction/InteractionZoneActionController.cs`
- `Assets/_Scripts/Interaction/InteractionZoneRuntimeState.cs`
- `Assets/_Scripts/Interaction/IInteractionActor.cs`
- `Assets/_Scripts/Interaction/InteractionActorResolver.cs`
- `Assets/_Scripts/Interaction/BuyZoneWorldUIBinder.cs`
- `Assets/_Scripts/Interaction/InteractionZoneUI.cs`

---

### 3. 생산 파이프라인 (Facility)

`FacilityBase`가 Template Method 패턴으로 공통 소비 루프를 정의하고, 세 시설이 훅을 구현한다.

```
FacilityBase.Update()
  ├── CanConsumeThisFrame()   : 입력 존 유무 + 인터벌 체크           (공통)
  ├── TryPrepareConsume()     : 가용 자원·수량 계산                  (공통)
  │     └── GetRemainingCapacity()                                   (서브클래스)
  ├── inputZone.AddStoredAmount(-n)                                  (공통)
  └── OnConsumed()            : 소비 후처리                          (서브클래스)
```

각 시설은 책임별 런타임 객체를 컴포지션으로 조립한다.

| 런타임 클래스 | 역할 |
|---|---|
| `FacilityStackViewRuntime` | 스택 뷰 스폰·배치·반환 (열/행/그리드/BoxCollider 영역 배치 지원) |
| `FacilityTimedProductionRuntime` | 인터벌 기반 입력→출력 변환 |
| `FacilityZoneCapacityRuntime` | Zone 용량 제한, Full 시 Zone 락/MAX 라벨 제어 |
| `FacilityZoneOutputRuntime` | Zone 출력 적재 + 뷰 수량 동기화 |
| `FacilityMaxLabelRuntime` | MAX 라벨 월드 UI (카메라 뒤 자동 숨김) |
| `CuffRailRuntime` | Cuff가 레일 경로를 따라 이동하는 연출 |
| `DeskPrisonerSupplyRuntime` | Prisoner별 Cuff 지급 수량 추적 |

| 시설 | 입력 | 출력 | 구현 특이사항 |
|---|---|---|---|
| `CuffFactory` | Ore (Submit Zone) | Cuff (Collect Zone) | 레일 이동 연출, 입/출력 버퍼 스택 뷰, MAX 라벨 2종 |
| `DeskFacility` | Cuff (Submit Zone) | Money (Collect Zone) + Prisoner 통과 | Prisoner별 Cuff 누적 지급, 통과 시 Money 보상 배출 |
| `JailFacility` | Prisoner | — | GridArea 슬롯 점유·해제, 업그레이드로 2구역 개방, 문 코루틴 애니메이션 |

Miner는 `CuffFactory.SubmitOreFromRemote()`로 플레이어를 거치지 않고 직접 공장에 광석을 적재한다.

관련 소스
- `Assets/_Scripts/Facility/FacilityBase.cs`
- `Assets/_Scripts/Facility/CuffFactory.cs` · `CuffRailRuntime.cs`
- `Assets/_Scripts/Facility/DeskFacility.cs` · `DeskPrisonerSupplyRuntime.cs`
- `Assets/_Scripts/Facility/JailFacility.cs` · `NpcGridArea.cs`
- `Assets/_Scripts/Facility/FacilityStack*.cs` · `FacilityZone*.cs` · `FacilityMax*.cs`

---

### 4. 채굴 시스템 (Mine / MineArea / ResourceManager)

`MineArea`는 XCount × YCount 그리드를 정의하고 BoxCollider가 있으면 그 크기를 셀 위치 계산에 사용한다. `ResourceManager`(Singleton)가 Awake 시 전체 셀에 Mine을 스폰하고 `Mine.Depleted` 이벤트를 구독한다.

Mine이 소진되면 `SetActive(false)` → `CoRespawnMine` 코루틴으로 `RespawnSeconds`(8초) 대기 후 해당 셀에 재스폰한다. Mine은 `IPoolable`을 구현해 풀에서 꺼낼 때 HP가 자동 리셋된다.

Mine 파괴 시 파편(MineDebrisPiece) N개를 랜덤 방향·크기·수명으로 스폰하는 연출을 수행한다. 파편도 `PooledViewBridge`로 풀에서 꺼내고 수명 후 반환한다.

장비별 채굴 정책:
- **Pickaxe**: 스택이 MAX이면 채굴 중단
- **Drill / Bulldozer**: MAX여도 파괴는 계속 진행 (적재만 생략)
- **Bulldozer**: 동시 3개 채굴(`SimultaneousMineCount`), Range=10, Interval=0.05

`ResourceManager.TryGetMineInFront()`는 플레이어 전방 dot product 필터로 전방각 범위 내 최근접 Mine만 반환한다.

관련 소스
- `Assets/_Scripts/Resource/Mine.cs` · `MineDebrisPiece.cs`
- `Assets/_Scripts/Resource/MineArea.cs`
- `Assets/_Scripts/Resource/ResourceManager.cs`
- `Assets/_Scripts/Resource/ResourceStack.cs`
- `Assets/_Scripts/Resource/ResourceData.cs`

---

### 5. NPC FSM

`NpcStateMachine`은 `Exit → Enter` 순서를 보장하는 24줄의 중재자다. `sealed`로 상속을 차단해 서브클래스가 순서를 깨뜨리는 것을 원천 방지한다.

```
Miner    : Wait → MoveToMine → Mine  (→ Wait 반복)
Worker   : Wait → MoveToCollect → Collect → MoveToSubmit → Submit  (→ Wait 반복)
Prisoner : MoveToQueue → MoveToReceive → WaitForCuff → MoveToPrison → InPrison
```

**Miner Mine 할당 (MinerManager)**  
`Dictionary<Mine, Miner>` + `Dictionary<Miner, Mine>` 쌍방 맵으로 동일 Mine에 여러 Miner가 겹치지 않도록 보장한다. Mine 탐색 시 미할당·활성화된 Mine 중 최근접 1개를 거리² 비교로 선택한다.

**Worker 운반 루프 (WorkerManager)**  
BuyWorker 완료 시 스폰. `_cuffFactory.CollectZone` → `_deskFacility.BoundInputZone` 경로를 인스펙터 설정 없이 코드로 직접 조회해 Worker에 전달한다. Worker는 자신의 운반 뷰(`_carryViews`)를 직접 관리하고 수집/제출 틱 간격을 독립적으로 설정할 수 있다.

**Prisoner 입장 직렬화 (PrisonerManager)**  
Jail 입구는 동시에 1명만 통과 가능하다. `_movingToJailEntrance` 토큰이 점유자를 관리하고, 비어 있을 때만 다음 Prisoner가 진행한다.

Prisoner 단계별 상세 흐름:

| 단계 | 상태 | 비고 |
|---|---|---|
| 스폰 후 Queue 이동 | `MoveToQueueState` | QueueSlot에 배정 |
| Queue → Receive 승격 | `MoveToReceiveState` | ReceiveSlot 비면 자동 승격 + 새 QueueSlot 스폰 |
| Cuff 수령 대기 | `WaitForCuffState` | `DeskFacility`가 인터벌마다 지급 |
| Ent Grid 이동 | `MoveToPrisonState` | 수령 완료 → NpcGridArea 슬롯 배정 |
| Jail 입구 이동 | `MoveToPrisonState` | `TryAcquireEntrance` → 입구 점유 후 이동 |
| Jail 슬롯 이동 | `MoveToPrisonState` | `CommitEnter` → GridArea 슬롯 배정 |
| 수감 완료 | `InPrisonState` | 최종 상태 |

Jail이 꽉 차도 EntSlot 수가 3 이하면 Cuff 수령을 계속 허용(`CanReceivePrisoner`)해 처리량이 과도하게 막히는 것을 방지한다. `UniquePrisonerQueue`(Queue + HashSet)로 Ent 대기열의 중복 입장을 방지한다.

관련 소스
- `Assets/_Scripts/NPC/NpcStateMachine.cs`
- `Assets/_Scripts/NPC/Prisoner/PrisonerManager.cs`
- `Assets/_Scripts/NPC/Prisoner/UniquePrisonerQueue.cs`
- `Assets/_Scripts/NPC/Miner/MinerManager.cs`
- `Assets/_Scripts/NPC/Worker/WorkerManager.cs`
- `Assets/_Scripts/NPC/Miner/States/` · `Worker/States/` · `Prisoner/States/`

---

### 6. 플레이어 시스템

**입력 통합**  
`FloatingJoystickInput.IsDragging`이 true이면 조이스틱 입력을 우선하고, 아니면 `InputActionAsset`("Player/Move") 값을 사용한다. 두 경로 모두 정규화된 `Vector2`로 합쳐져 `PlayerModel.ComposeMoveInput()`을 거쳐 `PlayerView.ApplyMove()`에 전달된다.

**자동 채굴**  
`TryAutoMineForwardMine()`이 매 Update 전방 범위 내 최근접 Mine을 탐색해 자동으로 `EquipBase.TryMine()`을 호출한다. 채굴 성공 시 산출 자원을 `ResourceStack.TryAdd()`로 적재하고 `PlayerCarryVisualizer.PlayIncomingTransfer()`로 아크 이동 연출을 재생한다.

**장비 연동**  
`EquipBase.LevelChanged` 이벤트가 발행되면 `PlayerController`가 `CarryCapacityBonus`를 재계산해 `PlayerCarryVisualizer.ApplyOreCarryCapacityBonus()`로 Ore 슬롯 용량을 갱신한다.

**적재 시각화 (PlayerCarryVisualizer)**

| 기능 | 구현 |
|---|---|
| 스택 뷰 관리 | `ResourceStack.Changed` 구독, 변경 자원만 뷰 동기화 |
| Sway 효과 | `PlayerCarrySwayCalculator`: 이동 시 관성·정지 시 바운스, 높이 지수 곡선 |
| 이동 연출 | `TransferFlight`: 포물선 아크(Sine 높이), End 위치 매 프레임 갱신 |
| 바운스 애니메이션 | 스택에 자원 추가 시 scale 1→peak→1 코루틴 |
| Money 겹침 방지 | Ore가 있을 때 Money 뷰를 뒤로 오프셋 이동 |

관련 소스
- `Assets/_Scripts/Player/PlayerController.cs`
- `Assets/_Scripts/Player/PlayerView.cs` · `PlayerModel.cs`
- `Assets/_Scripts/Player/PlayerCarryVisualizer.cs`
- `Assets/_Scripts/Player/PlayerCarryConfig.cs` · `PlayerCarrySwayCalculator.cs`

---

### 7. 장비 시스템 (EquipBase)

`EquipLevelLibrary` SO가 레벨 → EquipData 매핑을 관리한다. `EquipBase`는 현재 레벨에 맞는 뷰 프리팹을 Lazy Instantiate하고, Mine이 범위 안에 있을 때만 활성화한다.

장비 데이터:

| 장비 | Id | Range | Interval | Simultaneous | CarryBonus |
|---|---|---:|---:|---:|---:|
| Pickaxe | `pickaxe_lv1` | 3.5 | 0.4 | 1 | 0 |
| Drill | `drill_lv2` | 5.0 | 0.1 | 1 | +10 |
| Bulldozer | `bulldozer_lv3` | 10.0 | 0.05 | 3 | +20 |

Pickaxe 판별은 Id 접두어 비교(`StartsWith("pickaxe")`)로 처리한다.  
`EquipVisualPresentation`이 채굴 애니메이션·타격 이펙트·소진 이펙트를 담당한다.

관련 소스
- `Assets/_Scripts/Equip/EquipBase.cs`
- `Assets/_Scripts/Equip/EquipData.cs` · `EquipLevelLibrary.cs`
- `Assets/_Scripts/Equip/EquipPresentationBase.cs` · `EquipVisualPresentation.cs`

---

### 8. Object Pooling

`PoolManager`가 프리팹 키 기준 `Dictionary<GameObject, Queue<GameObject>>`로 풀을 관리한다.

- **Prewarm**: 초기화 시점에 N개를 미리 생성해 비활성 상태로 적재
- **Spawn**: 큐에 있으면 Dequeue, 없으면 Instantiate. `IPoolable.OnSpawned()` 호출
- **Despawn**: `IPoolable.OnDespawned()` → `SetActive(false)` → 루트 Transform 이동 → Enqueue
- **Queue FIFO**: 오브젝트를 골고루 재사용해 상태 초기화 안정성 확보

`PooledViewBridge`(정적 유틸)는 `PoolManager.Instance` 존재 여부를 호출자가 알 필요 없게 한다. Pool 없으면 `Instantiate`/`Destroy`로 폴백한다. `ReleaseAll(List<GameObject>)`로 리스트 전체를 한 번에 반환할 수 있다.

관련 소스
- `Assets/_Scripts/System/ObjectPooling/PoolManager.cs`
- `Assets/_Scripts/System/ObjectPooling/PooledViewBridge.cs`
- `Assets/_Scripts/System/ObjectPooling/IPoolable.cs`
- `Assets/_Scripts/System/ObjectPooling/PoolMember.cs`

---

### 9. 오디오 시스템

`AudioManager`(Singleton)가 2D SFX 풀과 3D World SFX 풀을 분리 관리한다.

| 풀 | spatialBlend | 대상 |
|---|---|---|
| 2D SFX (×8) | 0 | UI·글로벌 알림음 |
| 3D World SFX (×8) | 0.85 · Linear Rolloff | 채굴음·공장음 등 위치 기반 |

라운드 로빈으로 소스를 선택하고, 3D SFX는 소스 Transform을 월드 위치로 이동시켜 거리 감쇠를 적용한다.

`TryPlaySFX` / `TryPlayWorldSFX` 정적 진입점이 `Instance == null`을 조용히 무시해 외부 호출 시 null 체크 불필요. `IsInMainCameraView()`로 화면 밖 3D SFX 재생을 생략한다.

SFX ID 매핑:

| ID | 클립 | 사용처 |
|---|---|---|
| 0 | Bubble | 자원 적재·수집 |
| 1 | Cuff | CuffFactory 생산 |
| 2 | Bell | 엔딩 |
| 3 | MineHit | 곡괭이 채굴 타격 |
| 4 | MoneyCasher | 돈 획득 |
| 5 | MineCrash | Mine 파괴 |

관련 소스
- `Assets/_Scripts/Audio/AudioManager.cs`
- `Assets/_Scripts/Audio/SfxLibrary.cs`

---

### 10. 카메라 연출 시스템

`StageCameraDirectorTrigger`가 `StageProgressManager` 이벤트와 `CameraDirector` 사이의 어댑터다. 등록된 Entry를 순회해 조건이 맞는 Entry의 코루틴을 실행한다.

`CameraDirector`는 5단계 코루틴 파이프라인으로 동작한다:

```
startDelay
  → Zone 숨김 + onBeforePlay 콜백
  → [이동 travelDuration / AnimationCurve 보간]
  → holdDuration 대기
  → arrivalDelay → Zone 표시 · No Cell 버블 · onArrival 콜백
  → postArrivalDelay → 버블 숨김 · onBeforeReturn 콜백
  → [복귀 returnDuration, CameraController.TryGetFollowPose() 기준]
  → CameraController 재활성화
  → afterReturnDelay → onAfterReturn 콜백
```

복귀 목적지는 `CameraController.TryGetFollowPose()`로 현재 플레이어 위치 기준 포즈를 계산해 카메라가 끊기지 않고 자연스럽게 복귀한다.

각 Entry에는 UnityEvent 콜백 4종(`onBeforePlay`, `onArrival`, `onBeforeReturn`, `onAfterReturn`)이 있어 인스펙터에서 연출 중간에 임의 로직을 삽입할 수 있다.

씬 등록 연출 3종:

| 트리거 | Shot | 내용 |
|---|---|---|
| `CollectMoney` 첫 상호작용 | `Shot_FirstMoney_Drill` | BuyEquip Zone으로 카메라 안내, 도착 시 Zone 표시 |
| `OnJailBecameFull` | `Shot_JailFull_EntGrid` | No Cell 버블 + EntGrid 안내 (startDelay=3) |
| `BuyJail` 완료 | `Shot_JailExpand` | 감옥 확장 연출 후 `GameManager.ShowEndingPanel()` 호출 |

관련 소스
- `Assets/_Scripts/Camera/CameraDirector.cs`
- `Assets/_Scripts/Camera/CameraController.cs`
- `Assets/_Scripts/Camera/CameraShot.cs`
- `Assets/_Scripts/Camera/StageCameraDirectorTrigger.cs`

---

### 11. 커스텀 TMP 외곽선 컴포넌트

Unity 셰이더 기반 외곽선은 텍스트 테두리 자체가 두꺼워지는 문제가 있었다. 원본 TMP를 기준으로 외곽 레이어를 N개 자동 생성·동기화하는 컴포넌트로 해결했다.

- `BuildOffsets()`: 원형 분포 N개 오프셋 생성(2π/N 간격) → 레이어를 외곽선처럼 배치
- `_sampleCount`(레이어 수)와 `_strokeSize`(반경) 독립 설정으로 품질·성능 트레이드오프 제어
- 원본 텍스트 변경 시 내용·폰트·정렬·크기·마진 자동 동기화(`_autoSync = true`)
- 생성 레이어는 `LayoutElement.ignoreLayout = true`, `raycastTarget = false`로 동작 간섭 없음
- `HideFlags.DontSaveInEditor | DontSaveInBuild`로 씬 파일 오염 없음
- 에디터에서는 `EditorApplication.delayCall`로 지연 처리해 프리팹 자산 컨텍스트 충돌 방지
- 프리팹 복제 인스턴스끼리 레이어 이름이 충돌하지 않도록 `GetInstanceID()`를 Prefix에 포함
- Legacy Prefix 및 고아 레이어 자동 정리

관련 소스
- `Assets/_Scripts/UI/TMPExternalStroke.cs`

---

### 12. UI 시스템

| 클래스 | 역할 |
|---|---|
| `BuyZoneWorldUIBinder` | Zone.StateChanged → 진행 바·아이콘·수량 텍스트 갱신 |
| `MoneyUI` | ResourceStack.Changed 구독 → 돈 수량 표시, 플레이어 재바인딩 지원 |
| `MaxPopupUI` | "MAX" 팝업 (Singleton), 월드→Canvas 위치 추적, 카메라 뒤 숨김 |
| `PrisonerReceiveBubbleUI` | Prisoner 머리 위 말풍선 2종 (Receive 진행도 / Chat 텍스트), LateUpdate 위치 추적 |
| `StageGuideIndicatorUI` | 5단계 가이드 화살표 (화면 밖=방향 화살표, 화면 안=월드 바운스) |
| `EndingUI` | 로고→아이콘→Continue 순차 팝인 스케일 애니메이션 |
| `FloatingJoystickInput` | 터치/마우스 드래그 조이스틱, 정규화 입력 반환 |
| `PopInScale` | 활성화 시 0→peak→undershoot→1 스케일 애니메이션 |

`StageGuideIndicatorUI`는 5단계 진행(Mine 채굴 → CuffFactory Submit → Cuff Collect → Desk Submit → Money Collect)에 따라 가이드 화살표를 자동 전환한다. `CarryChanged`와 `ZoneStarted` 이벤트를 구독해 단계를 판단한다.

---

## 아키텍처 개요

```
[GameManager : Singleton]
  └── targetFrameRate = 60
  └── StageProgressManager.Initialize(FlowLibrary)
        ├── ZoneRegistry         : 씬의 모든 InteractionZone 수집 (ZoneId → Zone)
        ├── ZoneFlowController   : 초기 상태 적용 · 전이 실행 (HashSet 중복 방지)
        ├── ZonePurchaseUpgrade  : Zone 완료 → ConfigurePurchaseStep() 재설정
        └── StageStateMonitor    : JailFacility.StateChanged → EvaluateJailState()

[InteractionZone]
  ├── Started/Completed  → StageProgressManager, MinerManager, WorkerManager
  │                      → StageCameraDirectorTrigger (카메라 연출)
  └── StateChanged       → BuyZoneWorldUIBinder (UI)

[Player] ── 채굴 ──> [Mine (IPoolable)]
                       └── Depleted → ResourceManager → 비활성 + 리스폰 코루틴 (8초)

[Player/Miner] → SubmitOreZone  → [CuffFactory]   → CollectCuffZone
                                      └── CuffRailRuntime (레일 연출)
[Worker]       ─────────────────────────────────── → SubmitDeskZone
                                                       └── [DeskFacility]
                                                             ├── Money 보상 배출
                                                             └── Prisoner Cuff 지급

[PrisonerManager]
  Queue → Receive (DeskFacility Cuff 지급) → EntGrid → Jail입구 → [JailFacility]
                                                                    ├── StateChanged → StageStateMonitor
                                                                    └── 꽉 참 → OnJailBecameFull → BuyJail 활성

[StageCameraDirectorTrigger]
  StageProgressManager 이벤트 → CameraDirector.Play() (코루틴 5단계 파이프라인)

[AudioManager : Singleton]  2D/3D SFX 소스 풀 (×8 each), 라운드 로빈
[PoolManager : Singleton]   프리팹 키별 Queue<GameObject>, PooledViewBridge 폴백
[ResourceManager : Singleton] 셀별 Mine 스폰/리스폰 관리
```

---

## 폴더 구조

```
Assets/
├── _Scripts/
│   ├── System/
│   │   ├── GameManager.cs · Singleton.cs
│   │   ├── Stage/          StageProgressManager · ZoneFlowController
│   │   │                   ZonePurchaseUpgradeService · StageStateMonitor · ZoneRegistry
│   │   └── ObjectPooling/  PoolManager · PooledViewBridge · IPoolable · PoolMember
│   ├── Interaction/        InteractionZone · ActionController · FlowLibrary · Library
│   │                       IInteractionActor · ActorResolver · RuntimeState · UI · Binder
│   ├── Player/             Controller · View · Model
│   │                       CarryVisualizer · CarryConfig · CarrySwayCalculator
│   ├── Equip/              EquipBase · EquipData · EquipLevelLibrary
│   │                       PresentationBase · VisualPresentation
│   ├── Resource/           Mine · MineDebrisPiece · MineArea
│   │                       ResourceData · ResourceStack · ResourceManager
│   ├── Facility/           FacilityBase · CuffFactory · CuffRailRuntime
│   │                       DeskFacility · DeskPrisonerSupplyRuntime · JailFacility · NpcGridArea
│   │                       FacilityStackViewRuntime · FacilityStackUtility
│   │                       FacilityTimedProductionRuntime · FacilityZoneCapacityRuntime
│   │                       FacilityZoneOutputRuntime · FacilityMaxLabelRuntime
│   ├── NPC/
│   │   ├── NpcStateMachine · NpcState · INpcState · NPC
│   │   ├── Miner/          Miner · MinerManager · States/
│   │   ├── Worker/         Worker · WorkerManager · States/
│   │   └── Prisoner/       Prisoner · PrisonerManager · UniquePrisonerQueue · States/
│   ├── Camera/             CameraDirector · CameraController · CameraShot
│   │                       StageCameraDirectorTrigger
│   ├── Audio/              AudioManager · SfxLibrary
│   └── UI/                 TMPExternalStroke · FloatingJoystickInput
│                           EndingUI · MoneyUI · MaxPopupUI · PopInScale
│                           PrisonerReceiveBubbleUI · StageGuideIndicatorUI
├── _Data/                  SO 에셋 (FlowLibrary / ZoneLibrary / Equip / Resource / Camera / SFX)
├── _Prefabs/               Player / NPC / Facility / Resource / InteractionZone / UI
└── Scenes/
    └── Game.unity          단일 씬 (씬 Override로 밸런스 최종 튜닝)
```

---

## 개선 과제

- **자동화 테스트 부재** — Zone 전이·FSM 상태 전환·Jail 용량 관련 PlayMode 테스트 필요
- **직렬화 잔여 필드** — 리팩터링 후 씬/프리팹에 사용되지 않는 필드 흔적 잔류 (`FormerlySerializedAs` 정리 필요)
- **FindObjectsByType 사용** — `ZonePurchaseUpgradeService.ResolveEquip()`에서 초기화 시점에 사용. Direct Reference 캐싱으로 개선 여지
- **밸런스 소스 분산** — SO 기본값·Prefab 기본값·Scene Override 세 계층이 혼재해 최종 적용값 추적이 어려움

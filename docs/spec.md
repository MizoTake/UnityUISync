# Canvas UI Sync System 仕様書 v0.4

## 1. 概要

本システムは、同一 UI 構成を持つ複数台の Unity アプリケーション間で、**Canvas 単位**に UI 状態を同期するための仕組みを提供する。

主目的は以下とする。

* ライブ運用中の UI 操作を、複数端末間で安全に同期する
* 後から起動した端末も current state に追いつける
* シーン上のセットアップを最小限にする
* 危険な箇所は安全側に倒す

本システムは **current state の収束**を重視し、操作履歴の完全再生や完全配送保証は目的としない。

---

## 2. 設計方針

### 2.1 最重要方針

* シーン上で扱う MonoBehaviour は **Canvas ごとに 1 つ**
* 同期の正本は **Authority** が保持する
* 複数 Client は Authority に追従する
* 状態型 UI と操作型 UI を分けて扱う
* 後入り端末は snapshot で追いつく
* failover は v0.4 のスコープ外とする

### 2.2 トポロジ

v0.4 では full-mesh を採用せず、以下のスター構成とする。

* 1 `canvasId` ごとに 1 Authority
* その他は Client
* Client 同士は直接 state を同期しない
* snapshot 応答元は Authority のみ

### 2.3 同期の考え方

* 状態型 UI は current state を同期する
* Button は状態ではなく操作イベントとして同期する
* packet loss は起こりうる前提とし、次回 commit または再 snapshot で収束させる

---

## 3. スコープ

## 3.1 対象

* Unity UI / TMP UI の同期
* Canvas 単位の同期
* 2 台以上のマシン対応
* uOSC を用いた UDP / OSC 通信
* snapshot + delta 同期
* Button click 同期
* Authority / Client ロール分離

## 3.2 対象外

* 操作履歴の保存・再生
* 操作時刻どおりの再現
* 自動 failover
* 永続化された revision の復元
* 動的生成 UI の完全対応
* 信頼性保証付きトランスポート実装

---

## 4. シーン構成

## 4.1 MonoBehaviour

シーン上で使用する MonoBehaviour は **`CanvasUiSync` のみ**とする。

## 4.2 配置ルール

* `CanvasUiSync` は **Canvas の GameObject 自体**に付与する
* 付与された `CanvasUiSync` は **自身の Canvas 配下**のみを同期対象とする

## 4.3 1 Canvas = 1 同期単位

* 1 Canvas = 1 `canvasId`
* 1 Canvas = 1 registry
* 1 Canvas = 1 snapshot 範囲
* 複数 Canvas を同期したい場合は、それぞれに `CanvasUiSync` を付与する

---

## 5. 設定アセット

## 5.1 ScriptableObject

通信設定は ScriptableObject `CanvasUiSyncProfile` に保持する。

## 5.2 目的

* IP / Port / role の差し替えを容易にする
* 開発用 / 本番用 / 検証用設定を切り替えやすくする
* 同一設定を複数 Canvas で使い回せるようにする

## 5.3 保持項目

### 共通

* `profileName : string`
* `role : RoleType`

  * `Authority`
  * `Client`
* `nodeId : string`
* `protocolVersion : int`

### 共通タイミング設定

* `helloIntervalSeconds : float`
* `nodeTimeoutSeconds : float`
* `snapshotRequestIntervalSeconds : float`
* `snapshotRequestRetryCount : int`
* `snapshotRetryCooldownSeconds : float`

### 状態型 UI 送信制御

* `sliderEpsilon : float`
* `minimumProposeIntervalSeconds : float`
* `minimumCommitBroadcastIntervalSeconds : float`
* `stringSendMode : StringSendMode`

  * `OnEndEdit`
  * `OnValueChanged`

### Authority 用

* `listenPort : int`
* `allowDynamicJoin : bool`
* `allowedClients : List<string>`
* `clientEndpoints : List<RemoteEndpoint>`

### Client 用

* `listenPort : int`
* `authorityIpAddress : string`
* `authorityPort : int`

### ログ設定

* `verboseLog : bool`
* `logUnknownSyncId : bool`
* `logTypeMismatch : bool`
* `logDuplicateSyncId : bool`
* `logRegistryHashMismatch : bool`

## 5.4 RemoteEndpoint

* `name : string`
* `ipAddress : string`
* `port : int`
* `enabled : bool`

## 5.5 運用方針

* **既定は静的構成**
* `allowDynamicJoin = false` の場合、Authority は Profile に定義された Client のみ受け付ける
* `allowDynamicJoin = true` の場合、hello により動的参加を許可できる
* 本番運用では静的構成を推奨する

---

## 6. Canvas 識別子

## 6.1 canvasId

Canvas を識別するための論理 ID を `canvasId` とする。

## 6.2 既定値

* デフォルトでは **Canvas の GameObject 名**を `canvasId` として用いる

## 6.3 カスタム指定

* `canvasIdOverride : string` を指定できる
* `canvasIdOverride` が空でなければそれを優先する
* 空なら GameObject 名を使う

## 6.4 制約

* 同期対象となる各端末で `canvasId` は一致している必要がある
* 同一シーン内で同じ `canvasId` を持つ Canvas は禁止する

---

## 7. syncId 仕様

## 7.1 構成

`syncId` は以下を連結して自動生成する。

* `canvasId`
* Canvas から対象 UI までの階層パス
* `siblingIndex`
* `componentType`

例:

`OperationCanvas/LightingPanel[0]/MasterFader[2]:Slider`

## 7.2 目的

* 両端末で決定的に一致する ID を得る
* 同名兄弟の衝突を避ける

## 7.3 duplicate 時の挙動

* duplicate `syncId` は登録失敗
* error log を出す
* 当該 UI は同期対象外とする

---

## 8. 同期対象 UI

## 8.1 状態型 UI

* `Toggle` → `bool`
* `Slider` → `float`
* `Scrollbar` → `float`
* `Dropdown` → `int`
* `TMP_Dropdown` → `int`
* `InputField` → `string`
* `TMP_InputField` → `string`

## 8.2 操作型 UI

* `Button` → click event

## 8.3 snapshot 対象

snapshot に含めるのは **状態型 UI のみ**とする。

* Button は snapshot に含めない
* Button は click 時の delta のみ同期する

---

## 9. 自動スキャン

## 9.1 対象ルート

* `CanvasUiSync` が付与された Canvas の `Transform`

## 9.2 スキャン対象

* 対応済みの状態型 UI
* `Button`

## 9.3 スキャンタイミング

* `Start()` で 1 回
* `rescanOnEnable = true` の場合は `OnEnable()` でも再スキャン可能

## 9.4 v0.4 の制約

* 静的 UI を主対象とする
* ランタイム動的生成 UI は完全対応しない
* 必要なら後続バージョンで手動登録 API を追加する

---

## 10. ロール

## 10.1 Authority

Authority はその `canvasId` に対する正本を保持する。

責務:

* current state の保持
* revision の採番
* Client からの提案受理
* commit 配信
* snapshot 応答
* buttonSequence 採番
* node 管理

## 10.2 Client

Client は Authority に追従する。

責務:

* ローカル操作を propose として送信
* Authority の commit を反映
* snapshot 要求
* Authority 生存監視

---

## 11. 通信方式

## 11.1 トランスポート

* `uOSC` を利用する
* UDP / OSC を用いる

## 11.2 基本方針

* 完全配送保証は行わない
* current state 収束を優先する
* 欠落は再 snapshot で回復する

---

## 12. プロトコル

## 12.1 hello

アドレス:

`/uisync/hello`

引数:

* `nodeId`
* `protocolVersion`
* `canvasId`
* `role`
* `sessionId`

用途:

* join 通知
* heartbeat
* session 識別

## 12.2 requestSnapshot

アドレス:

`/uisync/requestSnapshot`

引数:

* `nodeId`
* `canvasId`
* `registryHash`

送信元:

* Client → Authority

用途:

* current state の再取得要求

## 12.3 beginSnapshot

アドレス:

`/uisync/beginSnapshot`

引数:

* `snapshotId`
* `canvasId`
* `authorityNodeId`
* `authoritySessionId`

送信元:

* Authority → Client

## 12.4 snapshotState

アドレス:

`/uisync/snapshotState`

引数:

* `snapshotId`
* `canvasId`
* `syncId`
* `type`
* `value`
* `stateRevision`

送信元:

* Authority → Client

## 12.5 endSnapshot

アドレス:

`/uisync/endSnapshot`

引数:

* `snapshotId`
* `canvasId`
* `authorityNodeId`
* `authoritySessionId`

送信元:

* Authority → Client

## 12.6 proposeState

アドレス:

`/uisync/proposeState`

引数:

* `nodeId`
* `canvasId`
* `syncId`
* `type`
* `value`
* `lastKnownRevision`

送信元:

* Client → Authority

用途:

* 状態型 UI の変更提案

## 12.7 commitState

アドレス:

`/uisync/commitState`

引数:

* `authorityNodeId`
* `authoritySessionId`
* `canvasId`
* `syncId`
* `type`
* `value`
* `stateRevision`

送信元:

* Authority → Client

用途:

* Authority が確定した状態の配信

## 12.8 proposeButton

アドレス:

`/uisync/proposeButton`

引数:

* `nodeId`
* `canvasId`
* `syncId`
* `localSequence`

送信元:

* Client → Authority

## 12.9 commitButton

アドレス:

`/uisync/commitButton`

引数:

* `authorityNodeId`
* `authoritySessionId`
* `canvasId`
* `syncId`
* `buttonSequence`

送信元:

* Authority → Client

---

## 13. セッション

## 13.1 sessionId

Authority は起動ごとに新しい `sessionId` を生成する。

## 13.2 目的

* Authority 再起動を Client が区別できるようにする
* revision のリセットと矛盾しないようにする

## 13.3 Authority 再起動時

* `stateRevision` は 0 から再開してよい
* `buttonSequence` も 0 から再開してよい
* ただし `sessionId` は必ず更新する
* Client は新しい `sessionId` を検知したら、新セッションとみなして snapshot を再要求する

---

## 14. revision / sequence

## 14.1 stateRevision

* `stateRevision` は **canvasId + syncId 単位**で Authority が保持する
* 初期値は 0
* 状態が commit されるたびに単調増加する
* 永続化は行わない

## 14.2 buttonSequence

* `buttonSequence` は **canvasId + syncId 単位**で Authority が保持する
* 初期値は 0
* `commitButton` ごとに単調増加する

## 14.3 破棄条件

Client は以下を破棄してよい。

* 既知より古い `stateRevision`
* 同一または古い `buttonSequence`
* 異なる `authoritySessionId` で整合しない古い commit

---

## 15. 起動・接続シーケンス

## 15.1 Authority 起動

1. `CanvasUiSync` 初期化
2. Profile 読み込み
3. `canvasId` 決定
4. UI 自動スキャン
5. registry 構築
6. Authority state 初期化
7. `sessionId` 生成
8. hello を定期送信開始
9. Client からの接続を待機

## 15.2 Client 起動

1. `CanvasUiSync` 初期化
2. Profile 読み込み
3. `canvasId` 決定
4. UI 自動スキャン
5. registry 構築
6. hello を定期送信開始
7. Authority に `requestSnapshot` 送信
8. snapshot 適用
9. commit 受信待機

## 15.3 同時起動

* Client は Authority の hello を待たずに `requestSnapshot` を再送してよい
* Authority が準備完了後に snapshot を返す
* 最終的に current state に収束すればよい

---

## 16. 状態型 UI の同期ルール

## 16.1 離散操作系

対象:

* Toggle
* Dropdown
* TMP_Dropdown
* InputField
* TMP_InputField

ルール:

* ローカル操作時に propose 送信
* Authority が採用した値を commit
* Client は commit を受けて確定する

## 16.2 連続操作系

対象:

* Slider
* Scrollbar

### 操作中の定義

**操作中**とは、対象 UI に対して `PointerDown` が発生してから `PointerUp` までの間を指す。

### 実装上の要件

実装方法は問わないが、仕様上は以下を満たすこと。

* `PointerDown` を検知できること
* `PointerUp` を検知できること
* この区間を連続操作中として扱えること

### 同期ルール

* 操作中はローカル UI 表示を優先する
* 操作中に受信した同一 `syncId` の `commitState` は即時適用しない
* 操作中は propose を送ってよいが、UI 表示はローカル優先とする
* `PointerUp` 時に最終値を必ず propose する
* 操作終了後は Authority の最新 commit に収束する

### 目的

* ドラッグ中の巻き戻りを防ぐ
* ライブオペ時の体験を安定させる

---

## 17. スロットリング

## 17.1 Client → Authority propose

連続操作系 UI の propose は以下で間引く。

* `minimumProposeIntervalSeconds` 未満では送らない
* 値差分が `sliderEpsilon` 未満なら送らない
* ただし `PointerUp` 時の最終値は必ず送る

## 17.2 Authority → Client commit

Authority は commit 再配信を **既定動作として間引く**。

* 同一 `syncId` の commit は最新値だけを送ればよい
* `minimumCommitBroadcastIntervalSeconds` 未満では古い中間値を破棄してよい
* commit が密集した場合は最新値のみ配信する

## 17.3 離散操作系

離散操作系 UI は即送信を基本とする。

---

## 18. InputField 送信ルール

## 18.1 既定

* 既定は `OnEndEdit`

## 18.2 任意切替

* `CanvasUiSyncProfile.stringSendMode` により `OnValueChanged` を選択可能

## 18.3 推奨

* 本番運用では `OnEndEdit` を推奨する

---

## 19. Button 同期ルール

## 19.1 基本

* Button は状態ではなく操作イベントである
* snapshot には含めない

## 19.2 Client 操作時

1. click 検知
2. `proposeButton` 送信
3. Authority が `buttonSequence` を採番
4. `commitButton` を全 Client に配信
5. Client は新しい sequence のみ `onClick.Invoke()` を実行する

## 19.3 Authority 操作時

* Authority 上の click も内部的に `buttonSequence` を採番する
* Authority 自身には再適用しない
* Client 群へ `commitButton` を送る

---

## 20. Authority ローカル操作

Authority でローカル操作が行われた場合も、Client からの提案と同等の正規パスで扱う。

## 20.1 状態型 UI

* Authority state を更新する
* `stateRevision` を採番する
* Client 群へ `commitState` を配信する
* Authority 自身には再適用しない

## 20.2 Button

* `buttonSequence` を採番する
* Client 群へ `commitButton` を配信する
* Authority 自身には再適用しない

---

## 21. snapshot

## 21.1 送信元

* snapshot を返すのは Authority のみ

## 21.2 形式

* 1 state = 1 message
* JSON 一括送信は行わない

## 21.3 対象

* 状態型 UI のみ
* Button は含めない

---

## 22. ハートビート / ノード管理

## 22.1 hello は heartbeat を兼ねる

* **Authority / Client の双方が定期的に hello を送る**
* 片方向ではなく **双方向**とする

## 22.2 join / leave

* 初回 hello 受信で join
* `lastSeen` 更新を継続する
* `nodeTimeoutSeconds` 超過で leave とみなす

## 22.3 Authority 断検知

* Client は Authority からの hello が timeout した場合、Authority 断とみなす
* warning / error log を出す
* 同期保証は失われる

## 22.4 v0.4 の扱い

* failover は実装しない
* 自動昇格しない
* ローカル UI 操作は継続可能だが、同期保証はない

---

## 23. 再 snapshot 要求

Client は以下をトリガーに `requestSnapshot` を自発的に再送してよい。

* Authority の `sessionId` 変化を検知したとき
* 初回 snapshot 受信に失敗したとき
* 一定時間 commit を受信しておらず、再同期が必要と判断したとき
* 明らかな不整合を検知したとき

### v0.4 既定トリガー

最低限、以下を既定動作とする。

* 起動後 snapshot 未取得
* Authority 再起動検知
* `snapshotRequestRetryCount` 未満の再試行
* commit 欠落が疑われ、`snapshotRetryCooldownSeconds` を超過している場合

---

## 24. registryHash 不一致

## 24.1 意味

* `registryHash` は UI 構成差異の検知用

## 24.2 不一致時の挙動

v0.4 では以下を採用する。

* Authority は warning を出す
* snapshot は送ってよい
* 不一致の `syncId` は個別に無視される

## 24.3 理由

* ライブ現場では同一ビルド前提だが、完全拒否より warning + 継続の方が安全

---

## 25. エラー処理

## 25.1 unknown syncId

* log 設定に応じて warning
* 当該メッセージは破棄
* 同期全体は継続

## 25.2 type mismatch

* error log
* 当該メッセージは破棄
* 同期全体は継続

## 25.3 古い revision / sequence

* 破棄
* 必要に応じて verbose log

## 25.4 snapshot 中断

* 未完了 snapshot は破棄してよい
* 次回 `requestSnapshot` で回復する

---

## 26. ループ防止 / 再入防止

## 26.1 基本方針

* 受信反映中はローカル送信しない
* suppression は bool ではなく count で管理する
* `try/finally` で必ず解除する

## 26.2 Button

* `commitButton` 適用時は sequence により冪等性を担保する
* 同一 sequence の再実行はしない

---

## 27. セキュリティ寄りの簡易防御

v0.4 では最低限、以下を入れてよい。

* Authority は `allowedClients` にない `nodeId` を拒否可能
* Client は Authority 以外からの `commitState` / `commitButton` を無視する
* `canvasId` が一致しないメッセージは無視する

---

## 28. ログ

## 28.1 必須ログ

* node join / leave
* duplicate nodeId
* duplicate syncId
* snapshot begin / end
* snapshot served
* registryHash mismatch
* Authority timeout
* stale revision discard
* stale button sequence discard
* unauthorized sender reject

## 28.2 任意 verbose

* propose 送信
* commit 受信
* snapshot retry
* throttle による送信抑制

---

## 29. テスト項目

## 29.1 基本

* Authority / Client 間で Toggle が一致する
* Dropdown が一致する
* InputField が一致する
* Button が 1 回だけ実行される

## 29.2 多台数

* 3 台以上の Client が Authority に追従する
* 1 Client の変更が他全 Client に反映される
* Button が重複実行されない

## 29.3 起動順

* Authority 先起動で Client 後入り
* Client 先起動で Authority 後起動
* 複数 Client が順不同で参加

## 29.4 連続操作

* Slider 操作中に UI が巻き戻らない
* PointerUp 後に Authority commit に収束する
* propose / commit のスロットリングが効く

## 29.5 障害

* Authority timeout を検知できる
* snapshot 再要求で回復できる
* registryHash 不一致で warning が出る
* unknown syncId で落ちない

---

## 30. v0.4 の最終採用事項

* Canvas ごとに 1 つの `CanvasUiSync`
* 通信設定は `CanvasUiSyncProfile`
* 既定 `canvasId` は Canvas の GameObject 名
* `canvasIdOverride` で上書き可能
* 2 台以上対応
* full-mesh ではなく Authority スター構成
* snapshot 応答元は Authority のみ
* 状態型 UI は revision ベースで同期
* Button は sequence ベースで同期
* 連続操作系 UI はローカル優先、PointerUp 後に収束
* Client propose / Authority commit の両方で既定スロットリングあり
* hello は双方向 heartbeat
* commit 欠落時は Client が snapshot を再要求できる
* registryHash 不一致時は warning + 継続
* failover は v0.4 のスコープ外
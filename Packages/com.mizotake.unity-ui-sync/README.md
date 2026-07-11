# Unity UI Sync

Canvas 単位で Unity UI と TMP UI を同期するための UPM パッケージです。

この README は Package Manager から参照されることを前提に、導入手順と最低限のセットアップだけをまとめています。リポジトリ全体の説明やローカル検証手順はルートの `README.md` を参照してください。

## 導入

Unity Package Manager の `Add package from git URL...` に次を指定します。

```text
https://github.com/MizoTake/UnityUISync.git?path=/Packages/com.mizotake.unity-ui-sync
```

`manifest.json` に直接書く場合の例です。

```json
{
  "dependencies": {
    "com.hecomi.uosc": "https://github.com/hecomi/uOSC.git#upm",
    "com.mizotake.unity-ui-sync": "https://github.com/MizoTake/UnityUISync.git?path=/Packages/com.mizotake.unity-ui-sync"
  }
}
```

`com.unity.textmeshpro` と `com.unity.ugui` は通常の UPM 解決で取得される想定です。`com.hecomi.uosc` は利用プロジェクト側でも Git URL を明示しておく方が安全です。

## セットアップ

1. `Create > Unity UI Sync > プロファイル` で `CanvasUiSyncProfile` を作成します。
2. `nodeId`、`listenPort`、`allowedPeers`、`peerEndpoints` を設定します。
3. 同期したい `Canvas` に `CanvasUiSync` を付与して `profile` を割り当てます。
4. 別シーン間で同じ論理 Canvas として扱いたい場合だけ `Canvas ID 上書き` を設定します。
5. 現行実装では OSC 通信は常に有効です。同一 process 内のデモでも uOSC を通して同期します。

`CanvasUiSyncRemoteEndpoint.name` は相手の `nodeId` に合わせる前提です。

## サンプル

Package Manager から `Import Samples > Basic Setup` を実行すると、`Samples/Unity UI Sync/Basic Setup` 配下へサンプルが展開されます。

同梱サンプルには以下が含まれます。

- `Scenes/UnityUiSyncSample.unity`
- `Scenes/UnityUiSyncPerformanceSample.unity`
- `Profiles/PeerA.asset`
- `Profiles/PeerB.asset`

`UnityUiSyncSample.unity` は基本動作確認用、`UnityUiSyncPerformanceSample.unity` は 1 Canvas あたり 128 個の同期 UI と簡易オーバーレイを持つ測定用です。`PeerA` は `9000`、`PeerB` は `9001` を使用します。

## 同期 ID

現在の同期 ID は以下の優先順で決定されます。

1. `CanvasUiSyncBindingId` コンポーネントが付いた場合は `canvasId/bindingId:ComponentType`
2. 未設定の場合は Canvas から当該コンポーネントまでの GameObject 階層パス

同一親配下に同名 GameObject が複数ある場合は hierarchy 順で `[0]`, `[1]` の添字が付きます。階層変更や sibling 順の変更で ID が変わる可能性があるため、動的 UI や同名 UI では `CanvasUiSyncBindingId` の利用を推奨します。

## 同期除外 UI

`CanvasUiSync` Inspector の `同期除外 UI` に対象 Component を登録すると、その UI を同期対象から除外できます。

- 対象 GameObject に `CanvasUiSyncBindingId` があれば Binding ID を安定キーとして使います。
- Binding ID がなければ Canvas からの相対階層パスを安定キーとして使います。
- Toggle や Dropdown など同期 UI Component を選ぶと、その UI 種別だけを除外します。Dropdown の展開状態と項目 Toggle もまとめて除外されます。
- RectTransform など同期対象外の Component を選ぶと、同じ GameObject 上のすべての同期 UI を除外します。
- UI を破棄して再生成しても、同じ Binding ID または階層パスなら除外を維持します。階層を移動する動的 UI には `CanvasUiSyncBindingId` を設定してください。
- 除外を解除する場合は、Inspector のリスト要素自体を削除してください。参照が Missing になっても保存済みの安定キーは維持されます。

## Runtime API

`CanvasUiSync` はゲーム側の C# コードから操作できます。API は Unity のメインスレッドから呼び出してください。

### 状態・Binding の参照と操作

```csharp
using System.Collections.Generic;
using Mizotake.UnityUiSync;
using UnityEngine;
using UnityEngine.UI;

var bindings = new List<CanvasUiSyncBindingInfo>();
uiSync.CopyBindings(bindings);

var result = uiSync.TrySetValue("OperationCanvas/PowerToggle:Toggle", true);
if (result != CanvasUiSyncApiResult.Succeeded)
{
    Debug.LogWarning(result);
}

uiSync.TryGetSynchronizedValue("OperationCanvas/PowerToggle:Toggle", out var value);
```

`TrySetValue(Component, object)` と `TryInvokeButton(Button)` も利用できます。これらは UI を直接書き換えて OSC を組み立てるのではなく、既存の stamp、送信間隔、除外、snapshot 制御を通します。

主な失敗結果は `NotInitialized`、`Inactive`、`SyncDisabled`、`Busy`、`Excluded`、`BindingNotFound`、`TypeMismatch`、`WrongThread` です。Collectionを読み取るAPIもメインスレッド専用で、別スレッドから呼ぶと `InvalidOperationException` を送出します。

### 動的 UI

```csharp
var runtimeToggle = Instantiate(togglePrefab, canvasTransform).GetComponent<Toggle>();
uiSync.NotifyHierarchyChanged();

// 同じフレーム中に Binding が必要な場合だけ即時更新します。
uiSync.RefreshBindingsNow();
uiSync.TrySetValue(runtimeToggle, true);
```

`NotifyHierarchyChanged()` は次の更新での再走査を予約する軽量 API です。`RefreshBindingsNow()` は Canvas 全体を即時走査します。動的 UI では `CanvasUiSyncBindingId` を付与し、peer 間で同じ Binding ID を使うことを推奨します。

### 通信操作

- `SendHelloNow()`
- `RequestSnapshotNow()`
- `ResynchronizeNow()`
- `SendSnapshotToPeer(nodeId)`
- `SetSyncEnabled(value)` / `EnableSync()` / `DisableSync()`

raw OSC の address や payload は公開していません。公開通信 API は既存プロトコルの形式とpeer設定を維持します。

### 設定の適用

```csharp
var runtimeProfile = Instantiate(profileTemplate);
runtimeProfile.nodeId = "RuntimePeer";
runtimeProfile.listenPort = 9100;
runtimeProfile.peerEndpoints[0].ipAddress = "192.168.0.20";
runtimeProfile.peerEndpoints[0].port = 9200;

uiSync.ApplyProfile(runtimeProfile);
uiSync.SetCanvasId("SharedOperationCanvas");
```

`ApplyProfile()` は渡された `ScriptableObject` を書き換えません。実行中に適用した場合はtransportを再設定し、新しいsessionを開始してBindingとsnapshot状態を再構築します。複数の `CanvasUiSync` で同じProfile assetを共有したまま個別設定を変更したい場合は、上記のように `Instantiate` したruntime copyを渡してください。

listen portを変更した場合はuOSCの再listen完了まで通信APIが`Busy`を返します。`GetStatus().TransportReady`が`true`になった後に送受信可能です。再listen完了時には`SynchronizationStateChanged`が発火し、Helloとsnapshot要求もその時点から開始します。

ProfileまたはCanvas IDを変更すると、接続済みpeerには `ConfigurationReset`、受信途中のsnapshotには `Cancelled` が通知されます。`SetCanvasId(null)` または空文字を渡すとCanvas ID上書きを解除し、GameObject名へ戻します。

### 通知

以下は購読可能な C# event です。

- `SynchronizationStateChanged`
- `BindingsRefreshed`
- `PeerStatusChanged`
- `StateApplied`
- `ButtonInvoked`
- `SnapshotStatusChanged`
- `DiagnosticRaised`

```csharp
uiSync.StateApplied += change =>
{
    Debug.Log($"{change.Origin}: {change.SyncId} = {change.Value}");
};

uiSync.PeerStatusChanged += change =>
{
    Debug.Log($"{change.Kind}: {change.Peer.NodeId}");
};
```

通知は内部状態の更新完了後にUnityメインスレッドで同期発火します。購読者が例外を送出しても、他の購読者と同期処理は継続します。

`StateApplied`と`ButtonInvoked`にはlogical ticks、node ID、sequenceを持つ`Stamp`が含まれます。`BindingsRefreshed`には通常／完全registry hash、`SnapshotStatusChanged`にはremote registry hashと互換性判定が含まれます。snapshotの`Started`には`Completed`、`TimedOut`、`RegistryMismatch`、`Cancelled`のいずれかが対応します。

通知callback内で`RefreshBindingsNow()`、`ApplyProfile()`、`SetCanvasId()`を再入実行すると`Busy`を返します。`StateApplied`から同じBindingへ無条件に`TrySetValue()`を呼ぶと循環するため、値または`Origin`を確認してください。

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

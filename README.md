# Unity UI Sync

`Unity UI` / `TextMeshPro UI` の状態を `Canvas` 単位で同期する、UI 系のデバッグツール兼 UPM パッケージです。

このリポジトリでは実装本体を [`Packages/com.mizotake.unity-ui-sync`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync) に配置しています。`CanvasUiSync` は `Toggle`、`Slider`、`Scrollbar`、`Dropdown`、`TMP_Dropdown`、`InputField`、`TMP_InputField`、`Button` を走査して同期対象として登録し、`OSC` または同一 process 内のローカル中継で状態を反映します。根拠は [`Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSync.Bindings.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSync.Bindings.cs) と [`Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSync.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSync.cs) です。

## これは何か

- 複数の UI 画面や peer 間で、同じ `Canvas` 構成の UI 状態を同期して比較・検証するためのツールです。
- `CanvasUiSyncProfile` に peer、ポート、再送、ログ設定を持たせ、`CanvasUiSync` にはシーン固有の `Canvas ID` 上書きだけを持たせる構成です。根拠は [`Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncEditor.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncEditor.cs) と [`Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncProfileEditor.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncProfileEditor.cs) です。
- `Last-Write-Wins` の競合解決、Snapshot 再同期、未知 `syncId` / 型不一致 / RegistryHash 不一致のログ、統計ログ出力に対応しています。根拠は [`Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSync.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSync.cs) と [`Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSyncProfile.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Runtime/CanvasUiSyncProfile.cs) です。

## 主な用途

- 別ノードの UI 操作が意図どおり反映されるかの確認
- UI の配線漏れ、型不一致、同期漏れの調査
- 実機接続前に Editor 内で peer 挙動を再現するローカル検証
- サンプルシーンを使ったデモ、再現手順の共有

## UPM 導入方法

### 1. Git URL で追加する

Unity Package Manager の `Add package from git URL...` に次を指定します。

```text
git@github.com:MizoTake/UnityUISync.git?path=/Packages/com.mizotake.unity-ui-sync
```

HTTPS を使う場合は次でも導入できる想定です。

```text
https://github.com/MizoTake/UnityUISync.git?path=/Packages/com.mizotake.unity-ui-sync
```

`manifest.json` に直接書く場合の例です。

```json
{
  "dependencies": {
    "com.mizotake.unity-ui-sync": "https://github.com/MizoTake/UnityUISync.git?path=/Packages/com.mizotake.unity-ui-sync"
  }
}
```

パッケージ名と依存関係の根拠は [`Packages/com.mizotake.unity-ui-sync/package.json`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/package.json) です。

### 2. 依存パッケージ

このパッケージは `com.hecomi.uosc`、`com.unity.textmeshpro`、`com.unity.ugui` に依存します。`package.json` に定義されているため、UPM 導入時に解決される前提です。

## 基本セットアップ

1. `Create > Unity UI Sync > プロファイル` で `CanvasUiSyncProfile` を作成します。
2. `nodeId`、`listenPort`、`allowedPeers`、`peerEndpoints` を設定します。
3. 同期したい `Canvas` に `CanvasUiSync` をアタッチして `profile` を割り当てます。
4. 必要なら `Canvas ID 上書き` を設定して、別シーンや別オブジェクトでも同じ論理 Canvas として扱います。
5. `enableOscTransport` をオフにすると、同一 process 内の peer を直接中継して Editor 上で確認できます。

`CanvasUiSyncRemoteEndpoint` は `name` を相手の `nodeId` に合わせる前提です。根拠は [`Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncRemoteEndpointDrawer.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncRemoteEndpointDrawer.cs) です。

## デバッグツールとしての見方

- `verboseLog`、`logUnknownSyncId`、`logTypeMismatch`、`logDuplicateSyncId`、`logRegistryHashMismatch` で同期異常の追跡ができます。
- `enableStatisticsLog` を有効にすると、送受信メッセージ数、概算バイト数、GC 回数差分を定期的に記録できます。
- `rescanOnEnable` を有効にすると、動的に組み替える UI を `OnEnable` 時に再走査できます。
- `allowDynamicPeerJoin` をオフにしたまま `allowedPeers` を使うと、想定外ノードの受信を抑止できます。

## サンプル

`Tools/Unity UI Sync/Rebuild Sample Assets` でサンプルシーンと Profile を再生成できます。実装は [`Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncSampleBuilder.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Editor/CanvasUiSyncSampleBuilder.cs) にあります。

生成される主な成果物:

- `Assets/Scenes/UnityUiSyncSample.unity`
- `Assets/UnityUISyncSamples/Profiles/PeerA.asset`
- `Assets/UnityUISyncSamples/Profiles/PeerB.asset`
- `Packages/com.mizotake.unity-ui-sync/Samples~/Basic Setup`

サンプルでは左右 2 つの Canvas を並べ、片側の操作がもう片側へ反映される UI を構築します。

## テストと検証

エディタテストと PlayMode テストは [`Packages/com.mizotake.unity-ui-sync/Tests/Editor/CanvasUiSyncCoreTests.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Tests/Editor/CanvasUiSyncCoreTests.cs) と [`Packages/com.mizotake.unity-ui-sync/Tests/PlayMode/CanvasUiSyncPlayModeTests.cs`](/D:/UnityProject/UnityUISync/Packages/com.mizotake.unity-ui-sync/Tests/PlayMode/CanvasUiSyncPlayModeTests.cs) にあります。

この作業では `unicli` の存在確認とサーバー状態確認までは実施しました。

```powershell
unicli check
unicli status
.\codex-unicli-check.cmd
.\codex-unicli-check.cmd play
```

ただし、この環境では `unicli` 実行時に既存 Unity プロセスが別コマンドを実行中で、追加テストは完走できませんでした。関連ログには `Server is busy executing 'TestRunner.RunPlayMode'` と、別バッチ起動系ログに `It looks like another Unity instance is running with this project open.` が残っています。根拠は [`Logs/editmode.log`](/D:/UnityProject/UnityUISync/Logs/editmode.log) と [`Logs/codex-build.log`](/D:/UnityProject/UnityUISync/Logs/codex-build.log) です。

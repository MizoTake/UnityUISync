# Unity UI Sync

`Unity UI` / `TextMeshPro UI` の状態を `Canvas` 単位で同期する、UI 系のデバッグツール兼 UPM パッケージです。

実装本体は `Packages/com.mizotake.unity-ui-sync` にあり、`Assets/Scenes` と `Assets/UnityUISyncSamples` には動作確認用の生成サンプルを置いています。

## 何をするか

- `Toggle`、`Slider`、`Scrollbar`、`Dropdown`、`TMP_Dropdown`、`InputField`、`TMP_InputField`、`Button` を `Canvas` 配下から走査して同期対象として登録します。
- `CanvasUiSyncProfile` に peer 設定、ポート、再送、ログ設定を持たせ、`CanvasUiSync` にはシーン固有の `Canvas ID` 上書きなど最小限の情報だけを持たせます。
- 現行実装では同一 process 内のデモも含めて uOSC を通して同期します。

## リポジトリ構成

- `Packages/com.mizotake.unity-ui-sync`: Runtime / Editor / Tests / Samples を含む配布対象の UPM パッケージ
- `Assets/Scenes` と `Assets/UnityUISyncSamples`: `Tools/Unity UI Sync/Rebuild Sample Assets` で再生成する検証用サンプル
- `docs/spec.md`: 初期設計メモ。現行実装との差分があるため、実際の挙動確認は package README と Inspector 表示を優先してください

## UPM 導入

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

利用者向けの最小セットアップは `Packages/com.mizotake.unity-ui-sync/README.md` にまとめています。

## サンプル

- `Tools/Unity UI Sync/Rebuild Sample Assets` でローカル検証用のサンプルシーンと profile を再生成できます
- Package Manager からは `Import Samples > Basic Setup` で同等のサンプルを取り込めます
- `UnityUiSyncSample` は左右 2 つの Canvas を並べ、片側の操作がもう片側へ反映される構成です
- `UnityUiSyncPerformanceSample` は 1 Canvas あたり 128 個の同期 UI と簡易オーバーレイを持つ測定用シーンです

## テスト

UniCli が使える環境では次のコマンドで検証できます。

```powershell
unicli check
unicli exec Compile --json
.\codex-unicli-check.cmd
.\codex-unicli-check.cmd play
```

`codex-unicli-check.cmd` は引数なしで EditMode、`play` で PlayMode、`all` で両方を実行します。

テスト本体は次にあります。

- `Packages/com.mizotake.unity-ui-sync/Tests/Editor/CanvasUiSyncCoreTests.cs`
- `Packages/com.mizotake.unity-ui-sync/Tests/PlayMode/CanvasUiSyncPlayModeTests.cs`

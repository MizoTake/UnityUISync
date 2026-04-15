# Unity UI Sync

Canvas 単位で Unity UI と TMP UI を同期するための UPM パッケージです。

このリポジトリではパッケージ本体を `Packages/com.mizotake.unity-ui-sync` に置き、動作確認用のサンプルシーンと Profile は `Assets/Scenes` および `Assets/UnityUISyncSamples` に生成します。

Package Manager 向けには `Samples~/Basic Setup` へ同内容のサンプルを出力します。

## 同期 ID の運用について

現在の同期 ID は以下の優先順で決定されます。

1. `CanvasUiSyncBindingId` コンポーネントが付いた場合は `canvasId/bindingId:ComponentType`。
2. 未設定の場合は Canvas から当該コンポーネントまでの GameObject 階層パスを使います。
   同一親配下に同名 GameObject が複数ある場合は hierarchy 順で `[0]`, `[1]` の添字が付きます。

このため、実装側で `GameObject` の階層や名前を後から変更すると ID が変わる可能性があります。
とくに同名 sibling の順番を入れ替えると ID も変わります。
`/` 同名の UI の重複や動的に階層が変わるレイアウトでは、同期先を固定したいコンポーネントに `CanvasUiSyncBindingId` を付けて固定文字列を使う方が安全です。

<!-- mcp-name: io.github.bimwright/nwd-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/nwd-mcp.png" alt="nwd-mcp" width="180" />
</p>

<h1 align="center">nwd-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#機能とアーキテクチャ"><img src="https://img.shields.io/badge/Navisworks-2022--2027-2D9B9B" alt="Navisworks 2022-2027" /></a>
  <a href="#ツールサーフェス"><img src="https://img.shields.io/badge/MCP-33%20tools-6C47FF" alt="MCP tools" /></a>
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.vi.md">Tiếng Việt</a> · <a href="README.zh-CN.md">简体中文</a> · 日本語
</p>

`nwd-mcp` は、**Autodesk Navisworks Manage** 自動化のためのプロフェッショナルグレードの Model Context Protocol (MCP) ゲートウェイです。AI エージェントがローカルの stdin/stdout 上で Autodesk Navisworks Manage デスクトップセッションをクエリ、検査、ナビゲート、スクリプト操作できるようにします。

---

## 機能とアーキテクチャ
- **対応ホスト:** Autodesk Navisworks Manage のみ（Freedom および Simulate は非対応）。
- **対応バージョン:** 2022、2023、2024、2025、2026、2027。
- **2プロセスモデル:** 軽量な `.NET 8` コンソールサーバーがバージョン別のインプロセスプラグインと通信します。ワイヤトランスポートは全対応年（2022–2027）で **ループバック上の TCP NDJSON** を使用します。6つのプラグインシェルはすべて `.NET Framework 4.8` / `net48` をターゲットとします。（bimwright ファミリーの他製品とは異なり、nwd-mcp は Named Pipe トランスポートを**使用しません** — 全バージョンでループバック TCP 方式が一律に適用されます。）
- **セキュリティ第一:** セッションごとのランダム暗号トークン検証、TCP トランスポートのループバック専用バインド、モデルに返されるエラーメッセージにおける絶対ファイルパスのサニタイズ。
- **マルチインスタンスルーティング:** 実行中の複数の Navisworks Manage インスタンスを自動検出し、動的なターゲット切り替えをサポートします。

Navisworks は MCP コマンドごとに 1 枚のアクティビティカードを表示します。トーストは既定でオンで、直近の結果から 20 秒残ります（10 / 20 / 30 / 60）。ホバーで一時停止し、離れると間隔を最初から数え直します。意図的にホバーすると、現在のカードの結果を、各項目に枠線を付けたコンパクトな3行の表示領域に滑らかに展開します（ツール、結果、成功／失敗、ローカルの完了時刻を24時間形式 `HH:mm:ss` で表示）。カード内の全件を古い順に保持し、最初は下端の最新3件を表示します。RVTと同じ細いスクロールバーで上にスクロールすると過去の呼び出しを確認できます。離れると滑らかに折りたたまれます。新しい結果はすぐに追加され、下端を見ている場合は最新の呼び出しに追従します。過去の呼び出しを読んでいる場合はスクロール位置を保ち、下端に戻ると追従を再開します。branding がオフでも使え、動きを減らす設定にも対応します。長さを制限した要約はメモリ内だけに保持し、カードを閉じるかリセットすると破棄します。Record は有効にせず、生のリクエスト、結果JSON、スクリプトソースは保持しません。BIMwright のワードマークは Show branding をオンにするまで出ません。`nwd_health_check` はカードに出ません。リボンの **Bimwright** タブに **Toasts**、**Toast Brand**、**Record**、**Status** があります。**Record** は旧ゲートウェイのホスト側の既定値です。現在のゲートウェイは下記のサーバー側ログ設定を使います。`send_code` はソースではなく長さと SHA-256 を保存します。設定は `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json` です。`BIMWRIGHT_NWD_ENABLE_TOAST` と `BIMWRIGHT_NWD_RECORD_CALLS` は次回起動時にそれぞれのスイッチを上書きします。

**Status → トーストの位置:** 左／右と上／下を個別に選択します（既定：左上）。即時適用・保存し、角を変更するとドラッグ位置を消去します。**Allow dragging the toast** を有効にするとタイトル行をドラッグできます。通常のクリックは引き続きカードを閉じます。無効化しても保存位置は残り、再度有効にすると復元します。**Reset position** はオフセットを削除します。位置は Navisworks ウィンドウからの相対 DIP で保存し、モニターの作業領域内に収めます。下端に配置すると上方向に展開し、空きがなければ逆方向を使います。アニメーションは Windows の設定に従います。保存失敗は Status 内に表示し、新設定は現在のセッションで有効です。ツール／結果のツールチップには機密情報を伏せた結果、状態、完了時刻、取得できた実行時間を表示します。複数アプリのトースト重複調整は含みません。

---

## 現在のステータス

| コンポーネント | ステータス |
|---|---|
| MCP ゲートウェイサーバー (.NET 8) | ✅ 警告なしでビルド成功 (Debug + Release) |
| 単体テスト (155 xUnit) | ✅ 全テスト合格 |
| プラグインハンドラ実装 | ✅ 実稼働 Navisworks Manage セッションで検証済み |
| プラグインプロジェクト (net48) | ✅ Navisworks Manage SDK に対してコンパイル成功 |

> **注:** プラグインハンドラ層は実際の Navisworks .NET API 呼び出し（スタブや疑似データではなく）を使用しており、実稼働の Navisworks Manage インスタンスで動作確認済みです。

---

## インストール

[GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest) から `NwdMcp.Setup-*-win-x64.zip` を入手。v1.0.0 は Manage **2025** プラグイン入り。展開して `install.ps1`。MCP はインストール済み `nwd-mcp.exe` を指定。NuGet パッケージ `Bimwright.Nwd.Server` はサーバー専用で、Navisworks プラグインは含みません。セットアップ ZIP からインストールしてください。ZIP ですでにプラグインを導入済みで、サーバーをグローバル .NET ツール（コマンド `bimwright-nwd`）として使いたい場合のみ `dotnet tool install -g Bimwright.Nwd.Server` を使います。

よく使うフラグ: `--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`。

---

## ツールサーフェス

既定の起動ではすべてのツールセットを登録します。**33 のツール** です。`--read-only` では **21** です。すべてのツールは `nwd_*` プレフィックスを使用します。

### 1. ターゲット/メタツール (3)
* `nwd_list_available_targets` — 検出されたすべてのアクティブな Navisworks セッションを一覧表示します。
* `nwd_get_current_target` — サーバーが現在どのセッションを指しているかを報告します。
* `nwd_switch_target` — ゲートウェイを別のアクティブセッションに向けます。

### 2. ファイルツール (3)
* `nwd_list_recent_files` — 実行中の Manage 年の最近使ったファイルを、ファイルメニューの順で列挙します。
* `nwd_open_file` *(書き込み)* — ファイルを開きます。現在のファイルに未保存の変更があるときは、`discard_changes` が true でない限り拒否します。
* `nwd_import_model` *(書き込み)* — 開いているドキュメントにモデルを取り込みます。`append`（既定）は別モデルとして追加し、`merge` は結合します。

### 3. クエリ/読み取りツール (8)
* `nwd_health_check` — ホスト／ドキュメント状態、実行中のサーバー／プラグインのバージョン、ビルドIDと一致状態を確認します。既定はオフライン。`check_updates=true` は両コンポーネントを公式の安定版と比較します。ホスト不在でもサーバーIDを返します（`ok:false`、`host_connected:false`）。
* `nwd_get_document_info` — アクティブなドキュメント名、ファイルパス、モデル数を取得します。
* `nwd_get_model_statistics` — 要素数、モデル数、選択数を取得します。
* `nwd_get_model_tree` — 制限付きモデルツリーのノード階層を取得します。
* `nwd_get_item_properties` — 要素のプロパティカテゴリとプロパティリストを取得します。
* `nwd_batch_get_properties` — 複数の要素のプロパティを一度に取得します。
* `nwd_find_items` — 高度なプロパティ/カテゴリフィルタを使用して要素をクエリします。
* `nwd_find_items_by_name` — 表示名で要素を検索します。

### 4. 選択ツール (3)
* `nwd_get_current_selection` — アクティブなユーザー選択の要素 ID を取得します。
* `nwd_clear_selection` *(書き込み)* — アクティブな選択をクリアします。
* `nwd_select_items_by_search` *(書き込み)* — プロパティ/名前フィルタに一致するアイテムを選択します。

### 5. 選択セットツール (3)
* `nwd_list_sets` — フォルダを再帰的に検索して選択セットと検索セットを取得します。
* `nwd_get_selection_set_items` — セット内の要素を取得します。
* `nwd_execute_search_set` *(混合)* — 検索セットを実行します。オプションで一致結果を選択できます。

### 6. ビューポイント/ナビゲーションツール (4)
* `nwd_list_viewpoints` — 保存済みビューポイントとフォルダを列挙します。
* `nwd_get_current_viewpoint` — 現在のカメラとビューの状態を取得します。
* `nwd_goto_viewpoint` *(書き込み)* — ビューポートカメラを保存済みビューポイントに移動します。
* `nwd_save_viewpoint` *(書き込み)* — アクティブビューを名前付きビューポイントとして保存します。

### 7. 可視性ツール (2)
* `nwd_hide_items` *(書き込み)* — 指定された要素を表示/非表示にします。
* `nwd_unhide_all` *(書き込み)* — 非表示の全要素を表示状態にリセットします。

### 8. エスケープハッチスクリプティング (1)
* `nwd_send_code` *(書き込み)* — Navisworks の UI スレッドで C# スクリプトを実行します（`doc` はアクティブなドキュメント）。最後の式を `result`、コンソール出力を `stdout` として返します。実行中 Navisworks は待機します。

### 9. ToolBaker 管理ツール (6)
* `nwd_list_baked_tools` — 検証済みのコンパイル済み再利用可能ツールをすべて一覧表示します。
* `nwd_run_baked_tool` *(書き込み)* — 許可されたベイクドツールを名前とパラメータで実行します。
* `nwd_list_bake_suggestions` — 適応型ワークフロー提案を一覧表示します。
* `nwd_accept_bake_suggestion` *(書き込み)* — 提案されたツールを検証、コンパイル、デプロイします。
* `nwd_dismiss_bake_suggestion` *(書き込み)* — アクティブな提案を却下/スヌーズします。
* `nwd_create_bake_issue_draft` — 要求されたツールの GitHub イシュードラフトを生成します。

---

## ランタイム health と更新

ターゲット選択後、セッション開始時に `nwd_health_check(check_updates=true)` を1回呼び、安定版の更新を確認します。ネットワークが利用不可／許可されていない場合は既定のオフライン確認を使い、not checked/unknownのままにします。その後の通常確認はオフラインです。`setup_gateway`／`setup_plugin` はロード済みアセンブリのIDです。SHA-256は取得時のディスク上のファイルを観察するだけで、メモリの再ロードや発行元の信頼性を証明しません。IDのない旧プラグインはバージョン／ビルドを `unknown` とします。ホスト確認はUIスレッドを使うため、Navisworksがビジーならタイムアウトすることがあります。

`check_updates=true` は1つの公式安定版とタグのリビジョンを、合計5秒のネットワーク予算で確認します。`update.components.server` と `.plugin` を個別に確認してください。同じバージョンだけでは不十分で、証拠不足は `unknown` のままです。`checked` はリリース検索の成功だけを意味します。コンポーネントの `update_available` は意味的バージョンの比較で、falseやnull通知はリリースID一致の証明ではありません。ソースリビジョンの比較も成果物のバイト検証ではありません。

`update.notification` のコードと `agent_action` を確認します。`NEW_RELEASE_AVAILABLE` は新バージョン、`RELEASE_REVIEW_REQUIRED`／`RELEASE_IDENTITY_UNVERIFIED` は証拠のレビュー、`LOCAL_VERSION_AHEAD` は新しいローカル版の案内です。`UPDATE_CHECK_UNAVAILABLE` は外部の `lookup_reason` であり、ローカルの異常やインストール承認要求ではありません。`protected_components` を保持し、`protected_component_reasons` を説明してください。新しいローカル版はダウングレードしません。`unobserved_components` は接続／ランタイム確認が必要で、ホスト終了や破損を意味しません。`identity_observed` と `comparison_reason` はランタイム証拠不足とリリース検索失敗を区別します。

`update_guidance.mode` は `check_only`、`retry_check`、`review`、`none`、`prepare_update` です。`bundle_update_proposal_allowed=true` の `prepare_update` だけがインストール手順と承認要求を返します。既存の承認は再利用します。他のモードは確認／報告／レビューだけです。新しいローカル版、未解決の相手ビルド、タグ検索失敗や未観測の相手があれば全体ZIPの提案を止めます。healthはパッケージ取得、インストール、再起動、文書保存／終了を行いません。[AGENTS.md](AGENTS.md) のプレビュー、承認、バックアップと検証に従ってください。現在のインストーラーはbundle+serverの原子的ロールバックを提供しません。開発ビルドを自動置換しないでください。

Navisworks再起動とクライアント再接続後、**同じ接続**の新しい `initialize.serverInfo` と `tools/list` のメタデータ／スキーマ／アノテーションを `client_verification` と比較します。サーバーはクライアントのキャッシュ更新を確認できません。クライアント側の証拠がなければ未検証です。

## セーフティ設定

### 読み取り専用モード
`--read-only` フラグまたは `BIMWRIGHT_NWD_READ_ONLY=1` 環境変数により、厳格な読み取り専用モードを適用できます。
- 書き込み可能なツールセットはすべて登録から除外されます。
- 混合ツール（`nwd_execute_search_set` など）は読み取り専用パラメータ範囲を強制するよう変更され（`select=false`）、`read_only_enforced` 応答マーカーを出力します。
- 読み取り専用モードのツールサーフェスは正確に **21 ツール** です。`nwd_list_recent_files` は残り、`nwd_open_file` と `nwd_import_model` は外れます。

### ツール呼び出しの記録（Record）
呼び出しログは**既定でオフ**です。`--enable-call-log` はローカルツールと失敗を含む全 MCP 呼び出しを `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwd-mcp-calls.jsonl` に記録します。プラグインへ送るコマンドは同じフォルダーの `mcp-calls.jsonl` にもネイティブ結果を記録します。`--disable-call-log` は、このゲートウェイによる両ログの作成と追記を止めます。MCPB の `call_log` は既定で false で、同じフラグを渡します。

優先順位は CLI → `BIMWRIGHT_NWD_ENABLE_CALL_LOG` → サーバー JSON の `enableCallLog` → false です。対応するプラグインはリクエストごとの値を優先し、リボン/Status の **Record**、保存された `recordCalls`、`BIMWRIGHT_NWD_RECORD_CALLS` や他のゲートウェイの設定を変更しません。値を送らない旧ゲートウェイはホスト設定を使います。旧プラグインはこの値を認識しないため、対応するビルドを使ってください。`send_code` のパラメーターにはソースの代わりに `code_length` と `code_sha256` を保存します。

### 権限と auto mode
`nwd_send_code` を除くすべてのツールが MCP アノテーション（`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`）を宣言しています。任意の更新確認でGitHubに接続する `nwd_health_check` 以外はopen-worldがfalseです。healthは読み取り専用で非破壊的です。Claude Desktop はこれを使い、読み取りツールは一度許可すれば済み、破壊的なツール（`nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`）は毎回確認します。

Claude Code の auto mode は MCP 呼び出しごとに分類器でチェックします。読み取り専用ツールでこのチェックを省くには、`.claude/settings.json` の `permissions.allow` に追加してください（MCP エントリ名が `nwd-mcp` 以外なら置き換えてください）：

```json
{
  "permissions": {
    "allow": [
      "mcp__nwd-mcp__nwd_list_recent_files",
      "mcp__nwd-mcp__nwd_list_available_targets",
      "mcp__nwd-mcp__nwd_get_current_target",
      "mcp__nwd-mcp__nwd_health_check",
      "mcp__nwd-mcp__nwd_get_document_info",
      "mcp__nwd-mcp__nwd_get_model_statistics",
      "mcp__nwd-mcp__nwd_get_model_tree",
      "mcp__nwd-mcp__nwd_get_item_properties",
      "mcp__nwd-mcp__nwd_batch_get_properties",
      "mcp__nwd-mcp__nwd_find_items",
      "mcp__nwd-mcp__nwd_find_items_by_name",
      "mcp__nwd-mcp__nwd_get_current_selection",
      "mcp__nwd-mcp__nwd_clear_selection",
      "mcp__nwd-mcp__nwd_select_items_by_search",
      "mcp__nwd-mcp__nwd_list_sets",
      "mcp__nwd-mcp__nwd_get_selection_set_items",
      "mcp__nwd-mcp__nwd_execute_search_set",
      "mcp__nwd-mcp__nwd_list_viewpoints",
      "mcp__nwd-mcp__nwd_get_current_viewpoint",
      "mcp__nwd-mcp__nwd_goto_viewpoint",
      "mcp__nwd-mcp__nwd_list_baked_tools",
      "mcp__nwd-mcp__nwd_list_bake_suggestions",
      "mcp__nwd-mcp__nwd_create_bake_issue_draft"
    ]
  }
}
```

`mcp__nwd-mcp__*` は**使わないでください**。ワイルドカードは `nwd_send_code` とすべての書き込みツールをチェックなしで許可してしまいます。モデルのあるプロジェクトフォルダーでエージェントを起動し、ファイルを開く・変更するよう頼むときは正確なファイル名を示してください。

### ToolBaker の永続化
ToolBaker の SQLite ストレージ（`bake.db`）と使用状況監査ログ（`audit.jsonl`）は、以下のローカルパスに永続化されます：
```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\baked\
```

### レスポンスサイズ
ツールの結果はコンパクトな JSON です。64 KiB を超えるとサーバーは結果に `_response_warning` フィールドを追加します（256 KiB 超でさらに強い警告）。1 MiB の予算を超えると `RESPONSE_TOO_LARGE` で拒否されます——完了済みの書き込みツールは失敗を装わず、圧縮された成功サマリー（`response_compacted`、`mutation_applied`）を返します。
4 つのツールは `output=file` を受け付け、完全な結果を同一マシンのローカルファイルに書き出してパス・スキーマ・プレビューを返します：`nwd_get_model_tree`（JSON）、`nwd_batch_get_properties`（SQLite）、`nwd_find_items_by_name`（NDJSON）、`nwd_run_baked_tool`（形式は自動判定）。`nwd_send_code` に `output` パラメーターはなく、1 MiB を超える出力は同じ方式で自動的にファイルへ退避します。
Spill ファイルは `%LOCALAPPDATA%\Bimwright\nwd-mcp\spill` に保存され、24 時間保持・最大 50 ファイルで、アンインストール時に削除されます。

## ベンチマーク

Navisworks Manage 2025 上の 33 ツールすべての所要時間、結果サイズ、推定トークン、サイズポリシーの結果: [docs/benchmarks/2026-09-30-navisworks-manage-2025.md](docs/benchmarks/2026-09-30-navisworks-manage-2025.md)。

---

## ローカル開発とコンパイル
Autodesk Navisworks API DLL はこのリポジトリに**再配布されていません**。

- **サーバーとテスト:** Navisworks がなくても任意のマシンでビルドおよび実行できます。
  ```powershell
  dotnet test tests\Bimwright.Nwd.Tests\Bimwright.Nwd.Tests.csproj -c Debug
  ```
- **プラグインのコンパイル:** ローカルへの Navisworks Manage インストールが必要です。
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="C:\Program Files\Autodesk\Navisworks Manage 2026"
  ```
  Navisworks Manage がデフォルト以外のパスにインストールされている場合は、ヒントプロパティを上書きしてください：
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="D:\Autodesk\Navisworks 2026"
  ```

---

## bimwright ファミリー

AI アシスタントと BIM・CAD アプリケーションをつなぐオープンソースのツール。

**bimwright** は **BIM** と **wright** を組み合わせた名前です。wright は、ものを作る人や建てる人を表す古い英語で、*shipwright*（船大工）などに使われます。

- [**rvt-mcp**](https://github.com/bimwright/rvt-mcp) — Autodesk® Revit®
- [**dwg-mcp**](https://github.com/bimwright/dwg-mcp) — Autodesk® AutoCAD®
- [**nwd-mcp**](https://github.com/bimwright/nwd-mcp) — Autodesk® Navisworks®
- [**ipt-mcp**](https://github.com/bimwright/ipt-mcp) — Autodesk® Inventor®
- [**bim-wiki**](https://github.com/bimwright/bim-wiki) — ベトナム語優先の BIM 知識ベース

---

## ライセンス

Apache-2.0。詳細は [LICENSE](LICENSE) を参照してください。

Navisworks および Autodesk は Autodesk, Inc. の登録商標です。bimwright は独立したオープンソースプロジェクトであり、Autodesk, Inc. とは提携、支援、または承認の関係にありません。

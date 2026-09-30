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

Navisworks は MCP コマンドごとに 1 枚のアクティビティカードを表示します。トーストは既定でオンで、直近の結果から 20 秒残ります（10 / 20 / 30 / 60）。ホバーで一時停止し、離れると間隔を最初から数え直します。BIMwright のワードマークは Show branding をオンにするまで出ません。`nwd_health_check` はカードに出ません。リボンの **Bimwright** タブに **Toasts**、**Toast Brand**、**Record**、**Status** があります。**Record** はオンにするまでオフです。オンにすると、すべてのツール呼び出しを `%LOCALAPPDATA%\Bimwright\nwd-mcp\mcp-calls.jsonl` に追記します。`send_code` はソースではなく長さと SHA-256 を保存します。設定は `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json` です。`BIMWRIGHT_NWD_ENABLE_TOAST` と `BIMWRIGHT_NWD_RECORD_CALLS` は次回起動時にそれぞれのスイッチを上書きします。

---

## 現在のステータス

| コンポーネント | ステータス |
|---|---|
| MCP ゲートウェイサーバー (.NET 8) | ✅ 警告なしでビルド成功 (Debug + Release) |
| 単体テスト (78 xUnit) | ✅ 全テスト合格 |
| プラグインハンドラ実装 | ✅ 実稼働 Navisworks Manage セッションで検証済み |
| プラグインプロジェクト (net48) | ✅ Navisworks Manage SDK に対してコンパイル成功 |

> **注:** プラグインハンドラ層は実際の Navisworks .NET API 呼び出し（スタブや疑似データではなく）を使用しており、実稼働の Navisworks Manage インスタンスで動作確認済みです。

---

## インストール

[GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest) から `NwdMcp.Setup-*-win-x64.zip` を入手。v0.1.2 は Manage **2025** プラグイン入り。展開して `install.ps1`。MCP はインストール済み `nwd-mcp.exe` を指定。`dotnet tool install -g Bimwright.Nwd.Server` は使わないでください。

よく使うフラグ: `--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`。`nwd_send_code` は既定でオンです。`--disable-send-code`（または `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0`）と、Navisworks プロセスの `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0` でオフにします（[セーフティ設定](#セーフティ設定)）。

---

## ツールサーフェス

既定の起動ではすべてのツールセットを登録します。`nwd_send_code` を含む **33 のツール** です。`--disable-send-code` では **32**、`--read-only` では **21** です。すべてのツールは `nwd_*` プレフィックスを使用します。

### 1. ターゲット/メタツール (3)
* `nwd_list_available_targets` — 検出されたすべてのアクティブな Navisworks セッションを一覧表示します。
* `nwd_get_current_target` — サーバーが現在どのセッションを指しているかを報告します。
* `nwd_switch_target` — ゲートウェイを別のアクティブセッションに向けます。

### 2. ファイルツール (3)
* `nwd_list_recent_files` — 実行中の Manage 年の最近使ったファイルを、ファイルメニューの順で列挙します。
* `nwd_open_file` *(書き込み)* — ファイルを開きます。現在のファイルに未保存の変更があるときは、`discard_changes` が true でない限り拒否します。
* `nwd_import_model` *(書き込み)* — 開いているドキュメントにモデルを取り込みます。`append`（既定）は別モデルとして追加し、`merge` は結合します。

### 3. クエリ/読み取りツール (8)
* `nwd_health_check` — アクティブセッションのステータスとハートビートを確認します。
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
* `nwd_send_code` *(書き込み、既定でオン)* — インプロセス C# コードをコンパイルし、Navisworks API に対して実行します。

### 9. ToolBaker 管理ツール (6)
* `nwd_list_baked_tools` — 検証済みのコンパイル済み再利用可能ツールをすべて一覧表示します。
* `nwd_run_baked_tool` *(書き込み)* — 許可されたベイクドツールを名前とパラメータで実行します。
* `nwd_list_bake_suggestions` — 適応型ワークフロー提案を一覧表示します。
* `nwd_accept_bake_suggestion` *(書き込み)* — 提案されたツールを検証、コンパイル、デプロイします。
* `nwd_dismiss_bake_suggestion` *(書き込み)* — アクティブな提案を却下/スヌーズします。
* `nwd_create_bake_issue_draft` — 要求されたツールの GitHub イシュードラフトを生成します。

---

## セーフティ設定

### 読み取り専用モード
`--read-only` フラグまたは `BIMWRIGHT_NWD_READ_ONLY=1` 環境変数により、厳格な読み取り専用モードを適用できます。
- 書き込み可能なツールセットはすべて登録から除外されます。
- 混合ツール（`nwd_execute_search_set` など）は読み取り専用パラメータ範囲を強制するよう変更され（`select=false`）、`read_only_enforced` 応答マーカーを出力します。
- 読み取り専用モードのツールサーフェスは正確に **21 ツール** です。`nwd_list_recent_files` は残り、`nwd_open_file` と `nwd_import_model` は外れます。

### send_code
`nwd_send_code` は**既定でオン**です。`--read-only` でもこのツールは外れます。サーバーでは `--disable-send-code` または `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0`、Navisworks プロセスでは `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0` でオフにします。どちらか一方がオフなら実行されません。`--enable-send-code` または `=1` で再びオンになります。認識できない値はオフです。

### ツール呼び出しの記録（Record）
**Record** は**既定でオフ**です。リボンのトグル、Status のチェック、`nwdmcp.config.json` の `recordCalls`、`BIMWRIGHT_NWD_RECORD_CALLS` は、すべてのツールに対する一つのスイッチです。オンの間、各呼び出しは設定ファイルの隣の `mcp-calls.jsonl` に追記されます。`send_code` のソースは書かれず、行には `code_length` と `code_sha256` が入ります。

### ToolBaker の永続化
ToolBaker の SQLite ストレージ（`bake.db`）と使用状況監査ログ（`audit.jsonl`）は、以下のローカルパスに永続化されます：
```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\baked\
```

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

<!-- mcp-name: io.github.bimwright/nwd-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/nwd-mcp.png" alt="nwd-mcp" width="180" />
</p>

<h1 align="center">nwd-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#機能とアーキテクチャ"><img src="https://img.shields.io/badge/Navisworks-2022--2027-2D9B9B" alt="Navisworks 2022-2027" /></a>
  <a href="#ツールサーフェス"><img src="https://img.shields.io/badge/MCP-29%20or%2030%20tools-6C47FF" alt="MCP tools" /></a>
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

---

## 現在のステータス

| コンポーネント | ステータス |
|---|---|
| MCP ゲートウェイサーバー (.NET 8) | ✅ 警告なしでビルド成功 (Debug + Release) |
| 単体テスト (45 xUnit) | ✅ 全テスト合格 |
| プラグインハンドラ実装 | ✅ 実稼働 Navisworks Manage セッションで検証済み |
| プラグインプロジェクト (net48) | ✅ Navisworks Manage SDK に対してコンパイル成功 |

> **注:** プラグインハンドラ層は実際の Navisworks .NET API 呼び出し（スタブや疑似データではなく）を使用しており、実稼働の Navisworks Manage インスタンスで動作確認済みです。初回実行のチェックリストについては [walkthrough.md](walkthrough.md) を参照してください。

---

## インストール

[GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest) から `NwdMcp.Setup-*-win-x64.zip` を入手。v0.1.2 は Manage **2025** プラグイン入り。展開して `install.ps1`。MCP はインストール済み `nwd-mcp.exe` を指定。`dotnet tool install -g Bimwright.Nwd.Server` は使わないでください。

よく使うフラグ: `--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`。`nwd_send_code` は二重オプトイン — [セーフティ設定](#セーフティ設定) を参照。

---

## ツールサーフェス

フェーズ 1 では、全ツールセットを有効にした場合にデフォルトで **29 のツール** を提供し、--toolsets all と --enable-send-code を同時に指定した場合には **30 のツール** を提供します。すべてのツールは `nwd_*` プレフィックスを使用します。

### 1. ターゲット/メタツール (3)
* `nwd_list_available_targets` — 検出されたすべてのアクティブな Navisworks セッションを一覧表示します。
* `nwd_get_current_target` — サーバーが現在どのセッションを指しているかを報告します。
* `nwd_switch_target` — ゲートウェイを別のアクティブセッションに向けます。

### 2. クエリ/読み取りツール (8)
* `nwd_health_check` — アクティブセッションのステータスとハートビートを確認します。
* `nwd_get_document_info` — アクティブなドキュメント名、ファイルパス、モデル数を取得します。
* `nwd_get_model_statistics` — 要素数、モデル数、選択数を取得します。
* `nwd_get_model_tree` — 制限付きモデルツリーのノード階層を取得します。
* `nwd_get_item_properties` — 要素のプロパティカテゴリとプロパティリストを取得します。
* `nwd_batch_get_properties` — 複数の要素のプロパティを一度に取得します。
* `nwd_find_items` — 高度なプロパティ/カテゴリフィルタを使用して要素をクエリします。
* `nwd_find_items_by_name` — 表示名で要素を検索します。

### 3. 選択ツール (3)
* `nwd_get_current_selection` — アクティブなユーザー選択の要素 ID を取得します。
* `nwd_clear_selection` *(書き込み)* — アクティブな選択をクリアします。
* `nwd_select_items_by_search` *(書き込み)* — プロパティ/名前フィルタに一致するアイテムを選択します。

### 4. 選択セットツール (3)
* `nwd_list_sets` — フォルダを再帰的に検索して選択セットと検索セットを取得します。
* `nwd_get_selection_set_items` — セット内の要素を取得します。
* `nwd_execute_search_set` *(混合)* — 検索セットを実行します。オプションで一致結果を選択できます。

### 5. ビューポイント/ナビゲーションツール (4)
* `nwd_list_viewpoints` — 保存済みビューポイントとフォルダを列挙します。
* `nwd_get_current_viewpoint` — 現在のカメラとビューの状態を取得します。
* `nwd_goto_viewpoint` *(書き込み)* — ビューポートカメラを保存済みビューポイントに移動します。
* `nwd_save_viewpoint` *(書き込み)* — アクティブビューを名前付きビューポイントとして保存します。

### 6. 可視性ツール (2)
* `nwd_hide_items` *(書き込み)* — 指定された要素を表示/非表示にします。
* `nwd_unhide_all` *(書き込み)* — 非表示の全要素を表示状態にリセットします。

### 7. エスケープハッチスクリプティング (1)
* `nwd_send_code` *(書き込み、オプトイン)* — インプロセス C# コードをコンパイルし、Navisworks API に対して実行します。

### 8. ToolBaker 管理ツール (6)
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
- 読み取り専用モードのツールサーフェスは正確に **20 ツール** です。

### SendCode オプトイン
動的 C# スクリプティング（`nwd_send_code`）は**デフォルトで無効**です。MCP サーバーは `--enable-send-code` または `BIMWRIGHT_NWD_ENABLE_SEND_CODE=1` を指定して起動した場合にのみこの機能を公開します。このサーバー側ゲートがツール登録を防止する権限のある制御機構です。プラグインはさらに、自身の環境から `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1` を第2の文書化されたオプトインシグナルとして読み取ります。

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

AEC ツールチェーンのために手作業で作られた MCP ゲートウェイ — 単一のアーキテクチャ、予測可能 / 監査可能 / 可逆：

- [**rvt-mcp**](https://github.com/bimwright/rvt-mcp) — Autodesk® Revit®
- [**dwg-mcp**](https://github.com/bimwright/dwg-mcp) — Autodesk® AutoCAD®
- [**nwd-mcp**](https://github.com/bimwright/nwd-mcp) — Autodesk® Navisworks®
- [**ipt-mcp**](https://github.com/bimwright/ipt-mcp) — Autodesk® Inventor®
- [**bim-wiki**](https://github.com/bimwright/bim-wiki) — ベトナム語優先の BIM 知識ベース

---

## ライセンス

Apache-2.0。詳細は [LICENSE](LICENSE) を参照してください。

Navisworks および Autodesk は Autodesk, Inc. の登録商標です。bimwright は独立したオープンソースプロジェクトであり、Autodesk, Inc. とは提携、支援、または承認の関係にありません。

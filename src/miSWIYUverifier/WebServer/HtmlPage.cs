using System.Globalization;
using System.Text.Json;

namespace miSWIYUverifier.WebServer;

/// <summary>
/// Generates the single-page HTML UI. Dynamic values are injected via
/// simple string replacement — no Razor/Blazor dependency needed.
/// The page is static: it creates its own verification session per visitor
/// via the REST API (POST /api/verification) and polls its status.
/// The UI is bilingual (German / English); the language is chosen from the
/// browser's Accept-Language header with English as the fallback.
/// </summary>
public static class HtmlPage
{
    public static string Render(string? acceptLanguage)
    {
        var lang = PickLanguage(acceptLanguage);
        var t    = lang == "de" ? De : En;

        // Strings needed by the client-side script (dynamic status/buttons).
        var js = JsonSerializer.Serialize(new
        {
            waiting            = t["waiting"],
            received           = t["received"],
            errorPrefix        = t["errorPrefix"],
            unknown            = t["unknown"],
            expired            = t["expired"],
            networkErrorPrefix = t["networkErrorPrefix"],
            yes                = t["yes"],
            no                 = t["no"],
            male               = t["male"],
            female             = t["female"],
        });

        return Template
            .Replace("___LANG___",              lang)
            .Replace("___SUBTITLE___",          t["subtitle"])
            .Replace("___WAITING___",           t["waiting"])
            .Replace("___HINT___",              t["hint"])
            .Replace("___RESULT_TITLE___",      t["resultTitle"])
            .Replace("___LABEL_GIVEN___",       t["labelGiven"])
            .Replace("___LABEL_FAMILY___",      t["labelFamily"])
            .Replace("___LABEL_BIRTH___",       t["labelBirth"])
            .Replace("___LABEL_AGE18___",       t["labelAge18"])
            .Replace("___LABEL_SEX___",         t["labelSex"])
            .Replace("___LABEL_NATIONALITY___", t["labelNationality"])
            .Replace("___LABEL_BIRTHPLACE___",  t["labelBirthPlace"])
            .Replace("___BTN_SCAN_AGAIN___",    t["btnScanAgain"])
            .Replace("___BTN_NEW_REQUEST___",   t["btnNewRequest"])
            .Replace("___T_JSON___",            js);
    }

    /// <summary>
    /// Picks "de" or "en" from an Accept-Language header, honouring q-values.
    /// English is the fallback when neither is requested or the header is empty.
    /// </summary>
    public static string PickLanguage(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
            return "en";

        var ranked = acceptLanguage
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry =>
            {
                var parts = entry.Split(';', StringSplitOptions.TrimEntries);
                var code  = parts[0].ToLowerInvariant();
                var q     = 1.0;
                var qPart = parts.Skip(1)
                    .FirstOrDefault(p => p.StartsWith("q=", StringComparison.OrdinalIgnoreCase));
                if (qPart is not null &&
                    double.TryParse(qPart.AsSpan(2), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed))
                    q = parsed;
                return (code, q);
            })
            .OrderByDescending(x => x.q);

        foreach (var (code, _) in ranked)
        {
            if (code.StartsWith("de")) return "de";
            if (code.StartsWith("en")) return "en";
        }
        return "en";
    }

    private static readonly Dictionary<string, string> De = new()
    {
        ["subtitle"]           = "Scanne den QR-Code mit der swiyu App",
        ["waiting"]            = "Warte auf Wallet-Antwort …",
        ["hint"]               = "Öffne die swiyu App, scanne den QR-Code<br>und bestätige die Datenweitergabe.",
        ["resultTitle"]        = "Identität verifiziert",
        ["labelGiven"]         = "Vorname",
        ["labelFamily"]        = "Familienname",
        ["labelBirth"]         = "Geburtsdatum",
        ["labelAge18"]         = "Über 18",
        ["labelSex"]           = "Geschlecht",
        ["labelNationality"]   = "Nationalität",
        ["labelBirthPlace"]    = "Geburtsort",
        ["btnScanAgain"]       = "Neuen Scan starten",
        ["btnNewRequest"]      = "Neuer Request",
        ["received"]           = "Identität empfangen",
        ["errorPrefix"]        = "Fehler: ",
        ["unknown"]            = "Unbekannt",
        ["expired"]            = "QR-Code abgelaufen – bitte neuen Request starten",
        ["networkErrorPrefix"] = "Netzwerkfehler: ",
        ["yes"]                = "Ja",
        ["no"]                 = "Nein",
        ["male"]               = "männlich",
        ["female"]             = "weiblich",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["subtitle"]           = "Scan the QR code with the swiyu app",
        ["waiting"]            = "Waiting for wallet response …",
        ["hint"]               = "Open the swiyu app, scan the QR code<br>and confirm the data sharing.",
        ["resultTitle"]        = "Identity verified",
        ["labelGiven"]         = "Given name",
        ["labelFamily"]        = "Family name",
        ["labelBirth"]         = "Date of birth",
        ["labelAge18"]         = "Over 18",
        ["labelSex"]           = "Sex",
        ["labelNationality"]   = "Nationality",
        ["labelBirthPlace"]    = "Place of birth",
        ["btnScanAgain"]       = "Start new scan",
        ["btnNewRequest"]      = "New request",
        ["received"]           = "Identity received",
        ["errorPrefix"]        = "Error: ",
        ["unknown"]            = "Unknown",
        ["expired"]            = "QR code expired – please start a new request",
        ["networkErrorPrefix"] = "Network error: ",
        ["yes"]                = "Yes",
        ["no"]                 = "No",
        ["male"]               = "male",
        ["female"]             = "female",
    };

    // -------------------------------------------------------------------------
    // The template is a plain (non-interpolated) string, so CSS curly braces
    // do NOT need to be escaped. Dynamic values use ___PLACEHOLDER___ tokens.
    // -------------------------------------------------------------------------
    private static readonly string Template = """
        <!DOCTYPE html>
        <html lang="___LANG___">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>miSWIYUverifier</title>
          <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

            body {
              font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto,
                           Helvetica, Arial, sans-serif;
              background: linear-gradient(135deg, #fff5f5 0%, #ffeaea 100%);
              min-height: 100vh;
              display: flex;
              align-items: center;
              justify-content: center;
              padding: 24px;
            }

            .card {
              background: #ffffff;
              border-radius: 20px;
              box-shadow: 0 8px 40px rgba(213, 43, 30, 0.12);
              padding: 44px 40px 36px;
              max-width: 460px;
              width: 100%;
              text-align: center;
              transition: all 0.3s ease;
            }

            /* ── Header ── */
            .ch-flag { font-size: 32px; margin-bottom: 8px; }

            .brand {
              font-size: 11px;
              font-weight: 700;
              letter-spacing: 3px;
              text-transform: uppercase;
              color: #d52b1e;
              margin-bottom: 10px;
            }

            h1 {
              font-size: 24px;
              font-weight: 700;
              color: #3b0c08;
              margin-bottom: 6px;
            }

            .subtitle {
              font-size: 14px;
              color: #6b7280;
              margin-bottom: 28px;
            }

            /* ── QR code ── */
            .qr-wrap {
              display: inline-flex;
              align-items: center;
              justify-content: center;
              background: #fafafa;
              border: 2px solid #e5e7eb;
              border-radius: 16px;
              padding: 16px;
              margin-bottom: 22px;
              min-width: 252px;
              min-height: 252px;
            }

            .qr-wrap img {
              display: block;
              width: 220px;
              height: 220px;
              image-rendering: pixelated;
            }

            /* ── Status badge ── */
            .status {
              display: inline-flex;
              align-items: center;
              gap: 9px;
              padding: 8px 20px;
              border-radius: 999px;
              font-size: 14px;
              font-weight: 500;
              margin-bottom: 14px;
              transition: all 0.4s ease;
            }

            .status.waiting {
              background: #fffbeb;
              color: #d97706;
              border: 1px solid #fcd34d;
            }

            .status.done {
              background: #f0fdf4;
              color: #16a34a;
              border: 1px solid #86efac;
            }

            .status.error {
              background: #fef2f2;
              color: #dc2626;
              border: 1px solid #fca5a5;
            }

            .spinner {
              width: 14px;
              height: 14px;
              border: 2px solid #fcd34d;
              border-top-color: #d97706;
              border-radius: 50%;
              animation: spin 0.75s linear infinite;
              flex-shrink: 0;
            }

            @keyframes spin { to { transform: rotate(360deg); } }

            .hint {
              font-size: 13px;
              color: #9ca3af;
              line-height: 1.6;
            }

            /* ── Result card ── */
            .result {
              background: #f0fdf4;
              border: 1px solid #bbf7d0;
              border-radius: 14px;
              padding: 24px;
              text-align: left;
              margin-top: 18px;
              display: none;
            }

            .result h2 {
              font-size: 15px;
              font-weight: 700;
              color: #15803d;
              margin-bottom: 18px;
              display: flex;
              align-items: center;
              gap: 8px;
            }

            .portrait-wrap {
              display: none;
              text-align: center;
              margin-bottom: 18px;
            }

            .portrait-wrap img {
              width: 110px;
              height: auto;
              border-radius: 12px;
              border: 2px solid #bbf7d0;
              box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            }

            .field {
              display: flex;
              justify-content: space-between;
              align-items: baseline;
              padding: 9px 0;
              border-bottom: 1px solid #d1fae5;
            }

            .field:last-of-type { border-bottom: none; }

            .field-label {
              font-size: 12px;
              font-weight: 600;
              text-transform: uppercase;
              letter-spacing: 0.5px;
              color: #6b7280;
            }

            .field-value {
              font-size: 15px;
              font-weight: 600;
              color: #3b0c08;
              text-align: right;
            }

            .format-tag {
              display: inline-block;
              margin-top: 14px;
              font-size: 11px;
              background: #fee2e2;
              color: #b91c1c;
              padding: 3px 10px;
              border-radius: 999px;
              font-weight: 500;
            }

            .reset-btn {
              display: block;
              width: 100%;
              margin-top: 24px;
              padding: 12px 24px;
              background: #d52b1e;
              color: #fff;
              border: none;
              border-radius: 10px;
              font-size: 15px;
              font-weight: 600;
              cursor: pointer;
              transition: background 0.2s, transform 0.1s;
              letter-spacing: 0.3px;
            }
            .reset-btn:hover:not(:disabled) { background: #b02318; }
            .reset-btn:active:not(:disabled) { transform: scale(0.98); }
            .reset-btn:disabled { background: #9ca3af; cursor: not-allowed; }

            .scan-again-btn {
              display: block;
              width: 100%;
              margin-top: 20px;
              padding: 11px 24px;
              background: #16a34a;
              color: #fff;
              border: none;
              border-radius: 10px;
              font-size: 15px;
              font-weight: 600;
              cursor: pointer;
              transition: background 0.2s, transform 0.1s;
            }
            .scan-again-btn:hover:not(:disabled) { background: #15803d; }
            .scan-again-btn:active:not(:disabled) { transform: scale(0.98); }
            .scan-again-btn:disabled { background: #9ca3af; cursor: not-allowed; }
          </style>
        </head>
        <body>

          <div class="card">

            <div class="ch-flag">🇨🇭</div>
            <div class="brand">Swiss E-ID &middot; swiyu Public Beta</div>
            <h1>miSWIYUverifier</h1>
            <p class="subtitle">___SUBTITLE___</p>

            <!-- QR Code (wird per JS aus der eigenen Session geladen) -->
            <div id="qr-section">
              <div class="qr-wrap">
                <img id="qr-img" alt="swiyu verification QR code" />
              </div>
            </div>

            <!-- Status -->
            <div class="status waiting" id="status-badge">
              <div class="spinner" id="spinner"></div>
              <span id="status-text">___WAITING___</span>
            </div>

            <p class="hint" id="hint">___HINT___</p>

            <!-- Identity result -->
            <div class="result" id="result">
              <h2>&#10003;&ensp;___RESULT_TITLE___</h2>
              <div class="portrait-wrap" id="portrait-wrap">
                <img id="r-portrait" alt="Portrait" />
              </div>
              <div class="field">
                <span class="field-label">___LABEL_GIVEN___</span>
                <span class="field-value" id="r-given">&mdash;</span>
              </div>
              <div class="field">
                <span class="field-label">___LABEL_FAMILY___</span>
                <span class="field-value" id="r-family">&mdash;</span>
              </div>
              <div class="field">
                <span class="field-label">___LABEL_BIRTH___</span>
                <span class="field-value" id="r-birth">&mdash;</span>
              </div>
              <div class="field">
                <span class="field-label">___LABEL_AGE18___</span>
                <span class="field-value" id="r-age18">&mdash;</span>
              </div>
              <div class="field">
                <span class="field-label">___LABEL_SEX___</span>
                <span class="field-value" id="r-sex">&mdash;</span>
              </div>
              <div class="field">
                <span class="field-label">___LABEL_NATIONALITY___</span>
                <span class="field-value" id="r-nationality">&mdash;</span>
              </div>
              <div class="field">
                <span class="field-label">___LABEL_BIRTHPLACE___</span>
                <span class="field-value" id="r-birthplace">&mdash;</span>
              </div>
              <div class="format-tag" id="r-format"></div>
              <button class="scan-again-btn" onclick="newVerification()">
                &#8635;&ensp;___BTN_SCAN_AGAIN___
              </button>
            </div>

            <button class="reset-btn" id="reset-btn" onclick="newVerification()">
              &#8635;&ensp;___BTN_NEW_REQUEST___
            </button>

          </div>

          <script>
            const T = ___T_JSON___;
            const el = id => document.getElementById(id);

            // Jeder Besucher bekommt seine eigene Verification-Session.
            let currentId = null;
            let finished  = false;
            let creating  = false;

            function formatBool(v) {
              if (v === 'true')  return '✓ ' + T.yes;
              if (v === 'false') return '✗ ' + T.no;
              return v || '—';
            }

            // ISO/IEC 5218: 1 = male, 2 = female
            function formatSex(v) {
              if (v === '1') return T.male;
              if (v === '2') return T.female;
              return v || '—';
            }

            function showError(message) {
              finished = true;
              el('spinner').style.display = 'none';
              el('status-badge').className = 'status error';
              el('status-text').textContent = message;
            }

            function resetUi() {
              finished = false;
              el('qr-section').style.display = '';
              el('result').style.display = 'none';
              el('portrait-wrap').style.display = 'none';
              el('reset-btn').style.display = '';
              el('spinner').style.display = '';
              el('hint').style.display = '';
              el('status-badge').className = 'status waiting';
              el('status-text').textContent = T.waiting;
            }

            async function newVerification() {
              if (creating) return;
              creating = true;
              const btn = el('reset-btn');
              btn.disabled = true;
              resetUi();

              try {
                const resp = await fetch('/api/verification', { method: 'POST' });
                const d    = await resp.json();
                if (!resp.ok || d.error) throw new Error(d.error || 'HTTP ' + resp.status);

                currentId = d.id;
                el('qr-img').src = 'data:image/png;base64,' + d.qrBase64;
              } catch (e) {
                currentId = null;
                showError(T.errorPrefix + e.message);
              } finally {
                creating = false;
                btn.disabled = false;
              }
            }

            function showResult(data) {
              el('qr-section').style.display = 'none';
              el('spinner').style.display = 'none';
              el('hint').style.display = 'none';
              el('status-badge').className = 'status done';
              el('status-text').textContent = T.received;

              el('r-given').textContent       = data.givenName   || '—';
              el('r-family').textContent      = data.familyName  || '—';
              el('r-birth').textContent       = data.birthDate   || '—';
              el('r-age18').textContent       = formatBool(data.ageOver18);
              el('r-sex').textContent         = formatSex(data.sex);
              el('r-nationality').textContent = data.nationality || '—';
              el('r-birthplace').textContent  = data.birthPlace  || '—';
              if (data.portrait) {
                el('r-portrait').src = data.portrait;
                el('portrait-wrap').style.display = 'block';
              }
              if (data.credentialFormat) {
                el('r-format').textContent = 'Format: ' + data.credentialFormat;
              }
              el('result').style.display = 'block';
              el('reset-btn').style.display = 'none';
            }

            async function poll() {
              if (finished || !currentId || creating) return;
              try {
                const resp = await fetch('/api/verification/' + currentId + '/status');

                // Session/Verification abgelaufen (TTL erreicht)
                if (resp.status === 404) { showError(T.errorPrefix + T.expired); return; }

                const d = await resp.json();
                if (d.status === 'complete' || d.status === 'partial') {
                  finished = true;
                  const dataResp = await fetch('/api/verification/' + currentId + '/data');
                  const result   = await dataResp.json();
                  showResult(result.data || {});
                } else if (d.status === 'error') {
                  showError(T.errorPrefix + (d.error || T.unknown));
                }
              } catch (_) { /* server may be shutting down – ignore */ }
            }

            newVerification();
            setInterval(poll, 2000);
          </script>
        </body>
        </html>
        """;
}

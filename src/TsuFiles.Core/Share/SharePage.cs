// Trang gửi/nhận mà điện thoại mở sau khi quét mã QR (ADR-0004 S1/S2). Chạy được trên Safari 16 (iPhone 8): chỉ
// HTML + một script cùng nguồn (CSP script-src 'self'), XMLHttpRequest để có tiến độ tải lên, không thư viện ngoài.
// Tên tệp đưa vào HTML luôn qua HtmlEncode.

using System.Globalization;
using System.Text;

namespace TsuFiles.Core.Share;

internal static class SharePage
{
    public static string Html(IReadOnlyList<string> sharedNames, long maxUploadBytes)
    {
        var files = new StringBuilder();
        for (var i = 0; i < sharedNames.Count; i++)
            files.Append(CultureInfo.InvariantCulture,
                $"<li><a href=\"f/{i}\" download>{Encode(sharedNames[i])}</a></li>");
        var list = sharedNames.Count > 0
            ? $"<ul class=\"ds\">{files}</ul>"
            : "<p class=\"phu\">Máy tính chưa chọn tệp nào để gửi.</p>";
        var max = (maxUploadBytes >> 20).ToString(CultureInfo.InvariantCulture);
        return $$$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="color-scheme" content="light dark">
            <title>TsuFiles - Gửi và nhận tệp</title>
            <style>
            :root{--nen:#fff;--chu:#111;--phu:#555;--vien:#ddd;--nhan:#6a2bd0}
            @media (prefers-color-scheme:dark){:root{--nen:#000;--chu:#eee;--phu:#aaa;--vien:#333;--nhan:#a06bf5}}
            body{margin:0;padding:16px;background:var(--nen);color:var(--chu);font:16px/1.5 -apple-system,system-ui,sans-serif}
            main{max-width:32rem;margin:0 auto}h1{font-size:1.4rem;margin:0 0 4px}h2{font-size:1.1rem;margin:24px 0 8px}
            .phu{color:var(--phu);font-size:.9rem}.ds{padding:0;list-style:none}.ds li{border-bottom:1px solid var(--vien)}
            .ds a{display:block;padding:12px 0;color:var(--nhan);word-break:break-all}
            label.nut{display:block;text-align:center;padding:14px;border-radius:10px;background:var(--nhan);color:#fff;font-weight:600}
            input[type=file]{position:absolute;opacity:0;width:1px;height:1px}
            #tt li{padding:8px 0;border-bottom:1px solid var(--vien);word-break:break-all}progress{width:100%}
            </style>
            </head>
            <body><main>
            <h1>TsuFiles</h1>
            <p class="phu">Chỉ dùng trong mạng tin cậy (Wi-Fi nhà, cơ quan). Đường truyền trong mạng nội bộ chưa mã hóa.</p>
            <h2>Gửi tệp tới máy tính</h2>
            <label class="nut" for="chon">Chọn ảnh hoặc tệp</label>
            <input id="chon" type="file" multiple>
            <p class="phu">Tối đa {{{max}}} MB mỗi tệp.</p>
            <ul id="tt" class="ds" aria-live="polite"></ul>
            <h2>Nhận tệp từ máy tính</h2>
            {{{list}}}
            </main>
            <script src="app.js"></script>
            </body>
            </html>
            """;
    }

    /// <summary>Chỉ thoát 5 ký tự đặc biệt của HTML; chữ tiếng Việt giữ nguyên.</summary>
    internal static string Encode(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal).Replace("'", "&#39;", StringComparison.Ordinal);

    public const string Script = """
        (function () {
          'use strict';
          var chon = document.getElementById('chon'), tt = document.getElementById('tt');
          function gui(tep, xong) {
            var li = document.createElement('li'), ten = document.createElement('div'), tien = document.createElement('progress');
            ten.textContent = tep.name; tien.max = 1; tien.value = 0;
            li.appendChild(ten); li.appendChild(tien); tt.appendChild(li);
            var x = new XMLHttpRequest();
            x.open('POST', 'u?name=' + encodeURIComponent(tep.name));
            x.upload.onprogress = function (e) { if (e.lengthComputable) tien.value = e.loaded / e.total; };
            x.onload = function () {
              li.removeChild(tien);
              ten.textContent = (x.status === 200 ? 'Đã gửi: ' : 'Lỗi: ') + (x.status === 200 ? x.responseText : x.responseText || x.status);
              xong();
            };
            x.onerror = function () { li.removeChild(tien); ten.textContent = 'Mất kết nối: ' + tep.name; xong(); };
            x.send(tep);
          }
          chon.addEventListener('change', function () {
            var ds = Array.prototype.slice.call(chon.files), i = 0;
            (function tiep() { if (i < ds.length) gui(ds[i++], tiep); else chon.value = ''; })();
          });
        })();
        """;
}

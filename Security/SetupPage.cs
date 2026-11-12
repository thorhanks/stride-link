using System.Net;

namespace StrideLink.Security;

public static class SetupPage
{
    public static string Render(string httpBase, string appUrl)
    {
        var certificateUrl = WebUtility.HtmlEncode($"{httpBase}/certificate");
        var safeAppUrl = WebUtility.HtmlEncode(appUrl);
        return $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
          <title>StrideLink Setup</title>
          <style>
            :root{color-scheme:dark;font-family:system-ui,-apple-system,sans-serif;background:#07110d;color:#ecfff5}
            body{margin:0;padding:28px 20px 48px}.wrap{max-width:580px;margin:auto}
            h1{font-size:2rem;margin:.25rem 0}.tag{color:#74e8ad;margin:0 0 28px}
            .card{background:#102019;border:1px solid #294d3c;border-radius:18px;padding:20px;margin:16px 0}
            .step{color:#74e8ad;font-weight:750}.note{color:#b7c9bf;line-height:1.5}
            a.button{display:block;text-align:center;background:#74e8ad;color:#06120c;text-decoration:none;font-weight:800;padding:17px;border-radius:14px;margin-top:14px}
            code{word-break:break-all;color:#b9ffd9}
          </style>
        </head>
        <body><main class="wrap">
          <h1>StrideLink setup</h1>
          <p class="tag">One-time secure sensor setup</p>
          <section class="card">
            <div class="step">1. Install the local certificate</div>
            <p class="note">Motion sensors require HTTPS. StrideLink generated this certificate authority and keeps its private key on your PC.</p>
            <a class="button" href="{{certificateUrl}}">Download certificate</a>
          </section>
          <section class="card">
            <div class="step">2. On iPhone, enable full trust</div>
            <p class="note">Open Settings → General → VPN &amp; Device Management, install “StrideLink Local Root CA,” then go to Settings → General → About → Certificate Trust Settings and enable full trust for it.</p>
            <p class="note">On Android, install the downloaded CA certificate when prompted. Exact wording varies by phone.</p>
          </section>
          <section class="card">
            <div class="step">3. Open StrideLink</div>
            <p class="note">Return here after trusting the certificate, then open the secure controller.</p>
            <a class="button" href="{{safeAppUrl}}">Open StrideLink</a>
          </section>
          <p class="note">Keep your PC and phone on the same private Wi-Fi network. Never install this certificate on a shared or untrusted device.</p>
        </main></body></html>
        """;
    }
}

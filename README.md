# StrideLink

StrideLink turns walking in place with a phone into a held **W** key on a Windows 11 PC. A tiny .NET console app hosts the phone interface over the local network, displays a QR code, receives walking state over a WebSocket, and uses Windows `SendInput` to control a game.

## v0.1 features

- Single terminal process; no traditional Windows UI
- LAN setup URL and scannable console QR code
- HTTPS mobile controller so iPhone/Android motion APIs can work
- One-time local certificate setup flow
- Manual hold-to-test button before motion calibration
- Orientation-independent accelerometer analysis and walking cadence estimate
- Sensitivity and stop-delay controls
- Screen wake lock where supported
- Private random session token
- Immediate W release on stop, disconnect, timeout, Ctrl+C, or shutdown
- Only the newest phone connection controls movement

### iPhone HTTPS hostname

The secure controller uses an address such as `192.168.1.47.nip.io`. The
hostname resolves to the embedded local IP address, allowing Safari to match
the DNS name in StrideLink's locally generated certificate. Controller traffic
still travels directly between the phone and PC over the LAN; only the DNS
lookup uses the public nip.io service.

Version 0.1.2 also loads the Windows TLS private key from a normal user key
container and explicitly supplies Kestrel with the issuing certificate chain.
The private key is tested at startup so a broken HTTPS credential fails with a
clear error instead of accepting TCP connections and then dropping TLS.

Version 0.1.3 corrects the native Win32 `INPUT` union size on 64-bit Windows so
`SendInput` accepts W-down and W-up events. A startup assertion protects the
required 40-byte x64 layout.

Version 0.1.4 maps the WebSocket endpoint without an HTTP-method restriction.
This supports both the traditional HTTP/1.1 GET upgrade and Safari's HTTP/2
extended CONNECT handshake instead of returning HTTP 405.

Version 0.1.5 fixes the browser/server message contract so the phone's
camelCase `type` and `active` fields are deserialized correctly. Manual and
motion events now reach the Windows W-key output.

## Run from source

Requirements: Windows 11 and the .NET 10 SDK.

```powershell
dotnet restore
dotnet run
```

Windows Defender Firewall may ask whether to allow access. Allow **Private networks** only. Scan the QR code while the PC and phone are on the same private Wi-Fi.

### One-time phone certificate setup

Mobile browsers require a trusted HTTPS page before they expose motion sensors. The QR code first opens an HTTP setup page that offers StrideLink's locally generated root certificate.

On iPhone:

1. Download the certificate from the setup page.
2. Open **Settings → General → VPN & Device Management** and install “StrideLink Local Root CA.”
3. Open **Settings → General → About → Certificate Trust Settings** and enable full trust for it.
4. Return to the setup page and tap **Open StrideLink**.

The certificate and its private key are generated locally under `%LOCALAPPDATA%\StrideLink\certificates`. The phone receives only the public root certificate. Remove the installed profile from the phone if you stop using StrideLink.

## Test in this order

1. Open Notepad on the PC.
2. Open the controller on the phone.
3. Hold **Hold to test W**. Notepad should type `w` characters until you release it.
4. Tap **Enable motion sensors**.
5. Put the phone securely in a front pants pocket and walk in place.
6. Tune **Sensitivity** if walking is missed or ordinary movement triggers it.
7. Only after the Notepad test works, try it in WoW.

If WoW is running as administrator, StrideLink must also run as administrator because Windows blocks lower-privilege processes from injecting input into elevated apps.

## Publish a Windows executable

The included script builds and packages the release:

```powershell
.\build.ps1
```

Or publish it directly:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

The output is under `bin\Release\net10.0\win-x64\publish`. Copy the entire publish folder for the first release; it includes the web assets next to `StrideLink.exe`.

## Options

```text
--ip 192.168.1.42     Override automatic LAN address selection
--http-port 5187      Setup page port
--https-port 5188     Secure controller port
```

If automatic address selection chooses a VPN adapter, launch with `--ip` and the PC's Wi-Fi/Ethernet IPv4 address (visible in `ipconfig`).

## Safety and limitations

- Keep the phone screen on and the controller page in the foreground.
- The PC and phone must stay on the same LAN.
- StrideLink releases W if it misses heartbeats for 1.6 seconds.
- v0.1 supports forward keyboard movement only; virtual controller output is intentionally deferred.
- Treat this as accessibility/game input, not unattended automation. The user must physically keep walking for movement to remain active.

## Development

The browser-side detector has dependency-free Node tests:

```powershell
npm test
npm run check
```

The QR code uses [QRCoder](https://www.nuget.org/packages/QRCoder). All other server functionality comes from ASP.NET Core and Windows APIs.

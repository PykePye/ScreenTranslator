# Snip & Translate

> A Claude Vision-powered screen translation tool for Windows. Press a hotkey, drag to select any region of your screen, get an instant translation.
>
> Công cụ dịch màn hình cho Windows dùng Claude Vision. Nhấn phím tắt, kéo chọn vùng cần dịch, nhận kết quả ngay lập tức.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![Powered by Claude](https://img.shields.io/badge/Powered%20by-Claude-D97757)](https://www.anthropic.com/claude)

---

## 🌐 Languages / Ngôn ngữ

- [English](#english)
- [Tiếng Việt](#tiếng-việt)

---

# English

## What is this?

Snip & Translate replaces the tedious workflow of taking a screenshot, opening a translation app, pasting the image, and waiting for results. Instead, you press one hotkey, drag a rectangle, and the translation appears next to the snipped region — automatically copied to your clipboard.

It was originally built to help factory workers operate foreign-language machine software (Chinese, Japanese, Korean HMIs) without needing to switch contexts every few seconds. But it works just as well for anyone who reads documentation, browses foreign-language websites, or plays games not yet localized to their language.

### Why Claude Vision instead of OCR?

Traditional OCR + translation pipelines fail on cluttered UIs, unusual fonts, and poorly rendered text — exactly the conditions you find in factory machine software and legacy applications. Claude's vision model handles complex layouts, understands UI context (a "Start" button on a CNC machine means something different than "Start" in an office app), and produces natural translations on the first try.

## Features

- **One-hotkey workflow** — Default `Ctrl+Shift+D`, customizable in Settings
- **Snipping Tool-style overlay** — Familiar UX, works across multiple monitors
- **Auto-detect source language** — Just point at the text, no need to specify what language it is
- **Multi-target translation** — Default to Vietnamese, change target on the fly via dropdown without re-snipping
- **Auto-copy to clipboard** — Result is ready to paste the moment it appears
- **Translation history** — Browse past translations with thumbnails, re-translate any snippet to a different language
- **Lives in the system tray** — Always ready, never in the way
- **Auto-starts with Windows** — Set it once, forget it
- **Pause mode** — Temporarily disable the hotkey when you don't need it

## Demo

> *Demo GIF/screenshot placeholder — add your own here*

## Tech Stack

- **C# / .NET 8** with WPF
- **Anthropic Claude API** (Sonnet 4.6 by default; Haiku 4.5 and Opus 4.7 selectable in Settings)
- **SQLite + Dapper** for translation history
- **Windows DPAPI** for API key encryption at rest

## Installation

### Prerequisites

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building from source)
- An [Anthropic API key](https://console.anthropic.com/) — create an account, generate a key, top up a few dollars (typical personal usage runs under $5/month)

### Build from source

```powershell
git clone https://github.com/PykePye/snip-and-translate.git
cd snip-and-translate
dotnet build
dotnet run
```

For a single-file release executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The binary will be at `bin\Release\net8.0-windows\win-x64\publish\ScreenTranslator.exe`.

## Configuration

On first launch, right-click the tray icon and choose **Settings**:

1. **API Key** — paste your Anthropic API key (encrypted via Windows DPAPI)
2. **Default Target Language** — typically your native language
3. **Model** — Sonnet 4.6 is the recommended default. Switch to Haiku 4.5 for faster, cheaper translations of simple UIs, or Opus 4.7 for dense screens with lots of small text
4. **Hotkey** — change the trigger combination if `Ctrl+Shift+D` conflicts with another app

## Usage

1. Press `Ctrl+Shift+D` anywhere in Windows
2. Drag to select the region containing the text you want to translate
3. Release the mouse — the translation appears next to your selection
4. Translation is automatically copied to your clipboard
5. (Optional) Use the dropdown on the result panel to translate the same image into a different language
6. Press `Escape` to dismiss the panel, or click anywhere outside it

To browse past translations: right-click the tray icon → Settings → History tab.

## Cost Expectations

Per-translation cost with Sonnet 4.6 (April 2026 pricing): roughly **$0.005–$0.01 per snip** for a typical screen region. That's about **100–200 translations per dollar**. Personal usage typically costs **under $5/month**.

## Privacy & Security

- The API key is stored encrypted using Windows DPAPI (`CurrentUser` scope), so the config file cannot be decrypted on a different machine or by a different user account.
- Snipped images are sent to Anthropic's API for translation. They are also stored locally in `%LocalAppData%\ScreenTranslator\history.db` for the History feature. You can disable history retention or set an auto-cleanup limit in Settings.
- No telemetry, no analytics, no third-party services other than the Anthropic API itself.

## Limitations

- **Windows only.** WPF and Win32 hotkey APIs are Windows-specific. No macOS or Linux port planned.
- **Requires internet.** No offline fallback; translation needs the Claude API.
- **Latency 2–4 seconds per translation.** Expected for a vision API call. Use Haiku 4.5 for faster turnaround.

## License

**Personal use only.** This is a personal project, shared publicly for reference and learning. You're welcome to read the code, fork it for your own personal experimentation, and learn from it. No commercial use, redistribution, or derivative product without explicit permission.

## Acknowledgments

- Built with [Claude](https://www.anthropic.com/claude) — both the API powering the translations and Claude as a coding partner during development
- Inspired by the daily workflow pain of bridging foreign-language factory software to Vietnamese-speaking operators

---

# Tiếng Việt

## Đây là gì?

Snip & Translate thay thế cái workflow rườm rà — chụp màn hình, mở ứng dụng dịch, paste ảnh, chờ kết quả — bằng một thao tác duy nhất: nhấn phím tắt, kéo chọn vùng, kết quả dịch hiện ra ngay cạnh vùng đã chọn và tự động copy vào clipboard.

Ban đầu công cụ này được viết để hỗ trợ công nhân nhà máy thao tác với các phần mềm vận hành máy bằng tiếng nước ngoài (HMI tiếng Trung, Nhật, Hàn) mà không cần liên tục chuyển qua lại giữa các ứng dụng. Nhưng nó cũng hữu ích cho bất cứ ai đọc tài liệu kỹ thuật, lướt web nước ngoài, hay chơi game chưa được Việt hóa.

### Tại sao chọn Claude Vision thay vì OCR?

Các pipeline OCR + dịch truyền thống thường thất bại với UI phức tạp, font chữ lạ, hoặc text được render không sắc nét — đúng những điều kiện thường gặp trong phần mềm máy móc nhà máy và các ứng dụng cũ. Vision model của Claude xử lý được layout phức tạp, hiểu được ngữ cảnh UI (nút "Start" trên máy CNC mang ý nghĩa khác với "Start" trên app văn phòng), và cho ra bản dịch tự nhiên ngay lần đầu.

## Tính năng

- **Một phím tắt cho mọi thứ** — Mặc định `Ctrl+Shift+D`, có thể đổi trong Settings
- **Overlay phong cách Snipping Tool** — UX quen thuộc, hoạt động trên đa màn hình
- **Tự động phát hiện ngôn ngữ nguồn** — Cứ chỉ vào text là được, không cần khai báo
- **Đa ngôn ngữ đích** — Mặc định tiếng Việt, đổi target ngay trên panel kết quả mà không cần snip lại
- **Tự copy vào clipboard** — Sẵn sàng paste ngay khi kết quả xuất hiện
- **Lịch sử dịch** — Xem lại các bản dịch cũ kèm thumbnail, dịch lại sang ngôn ngữ khác
- **Sống trong system tray** — Luôn sẵn sàng, không cản đường
- **Tự khởi động cùng Windows** — Setup một lần, dùng mãi
- **Chế độ tạm dừng** — Tắt phím tắt tạm thời khi không cần

## Demo

> *Chỗ này dán GIF/ảnh demo*

## Công nghệ sử dụng

- **C# / .NET 8** với WPF
- **Anthropic Claude API** (mặc định Sonnet 4.6; có thể chọn Haiku 4.5 hoặc Opus 4.7 trong Settings)
- **SQLite + Dapper** cho lịch sử dịch
- **Windows DPAPI** mã hóa API key

## Cài đặt

### Yêu cầu

- Windows 10 hoặc 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (nếu muốn build từ source)
- [API key của Anthropic](https://console.anthropic.com/) — tạo tài khoản, generate key, nạp vài đô (dùng cá nhân thường chưa tới $5/tháng)

### Build từ source

```powershell
git clone https://github.com/PykePye/snip-and-translate.git
cd snip-and-translate
dotnet build
dotnet run
```

Để build file exe single-file cho release:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

File exe sẽ nằm ở `bin\Release\net8.0-windows\win-x64\publish\ScreenTranslator.exe`.

## Cấu hình

Lần đầu chạy, click phải vào tray icon → **Settings**:

1. **API Key** — paste API key của Anthropic vào (sẽ được mã hóa bằng Windows DPAPI)
2. **Default Target Language** — ngôn ngữ đích mặc định, thường là tiếng mẹ đẻ
3. **Model** — Sonnet 4.6 là mặc định khuyến nghị. Chuyển sang Haiku 4.5 nếu muốn dịch nhanh, rẻ cho UI đơn giản, hoặc Opus 4.7 cho màn hình nhiều text nhỏ
4. **Hotkey** — đổi tổ hợp phím nếu `Ctrl+Shift+D` xung đột với app khác

## Cách sử dụng

1. Nhấn `Ctrl+Shift+D` ở bất kỳ đâu trên Windows
2. Kéo chuột chọn vùng chứa text cần dịch
3. Nhả chuột — kết quả dịch hiện ra cạnh vùng đã chọn
4. Bản dịch tự động được copy vào clipboard
5. (Tùy chọn) Dùng dropdown trên panel để dịch cùng ảnh đó sang ngôn ngữ khác
6. Nhấn `Escape` hoặc click ra ngoài để đóng panel

Để xem lại lịch sử: click phải tray icon → Settings → tab History.

## Chi phí dự kiến

Chi phí mỗi lần dịch với Sonnet 4.6 (giá tháng 4/2026): khoảng **$0.005–$0.01 mỗi lần snip** cho vùng màn hình thông thường. Tức là **$1 dùng được 100–200 lần dịch**. Sử dụng cá nhân thường **dưới $5/tháng**.

## Bảo mật & Quyền riêng tư

- API key được lưu mã hóa bằng Windows DPAPI (scope `CurrentUser`), nghĩa là file config không thể decrypt trên máy khác hoặc bằng tài khoản Windows khác.
- Ảnh snip được gửi tới API của Anthropic để dịch. Ảnh cũng được lưu trong `%LocalAppData%\ScreenTranslator\history.db` cho tính năng History. Có thể tắt lưu lịch sử hoặc đặt giới hạn auto-cleanup trong Settings.
- Không telemetry, không analytics, không bên thứ ba nào ngoài Anthropic API.

## Hạn chế

- **Chỉ chạy trên Windows.** WPF và Win32 hotkey API là Windows-specific. Không có kế hoạch port sang macOS/Linux.
- **Cần internet.** Không có fallback offline; cần API Claude để dịch.
- **Độ trễ 2–4 giây mỗi lần dịch.** Bình thường với vision API. Dùng Haiku 4.5 nếu muốn nhanh hơn.

## License

**Chỉ dùng cho mục đích cá nhân.** Đây là dự án cá nhân, share công khai để tham khảo và học hỏi. Bạn có thể đọc code, fork về tự thử nghiệm cá nhân, và học từ nó. Không sử dụng thương mại, không phân phối lại, không tạo sản phẩm phái sinh khi chưa có sự đồng ý rõ ràng.

## Lời cảm ơn

- Xây dựng với [Claude](https://www.anthropic.com/claude) — vừa là API cung cấp khả năng dịch, vừa là partner code trong suốt quá trình phát triển
- Lấy cảm hứng từ thực tế công việc: cầu nối giữa phần mềm máy móc tiếng nước ngoài và công nhân vận hành nói tiếng Việt

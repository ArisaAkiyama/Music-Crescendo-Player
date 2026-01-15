# Panduan Build & Packaging Desktop Music Player

Aplikasi Anda sudah siap secara fungsional. Dokumen ini menjelaskan cara mengubah source code menjadi file installer (`.exe`) yang siap didistribusikan ke pengguna lain.

## 1. Persiapan Akhir (Opsional tapi Disarankan)

### Tambahkan Icon Aplikasi
Agar terlihat profesional, tambahkan file icon (`.ico`) ke project.
1. Siapkan file `.ico` (misal `app_icon.ico`). Simpan di folder project utama.
2. Edit file `DesktopMusicPlayer.csproj` dan tambahkan:
   ```xml
   <PropertyGroup>
     ...
     <ApplicationIcon>app_icon.ico</ApplicationIcon>
   </PropertyGroup>
   ```

## 2. Publish Aplikasi (Membuat File EXE Utama)

Kita tidak hanya melakukan "Build", tapi "Publish" agar semua dependency (dll) terkumpul rapi.

Buka Terminal di folder project (`d:\Project\DesktopMusic`) dan jalankan:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
```

*   `-c Release`: Mode optimasi (lebih cepat, tanpa debug info).
*   `-r win-x64`: Target Windows 64-bit.
*   `--self-contained false`: User perlu install .NET Runtime (ukuran installer kecil). Jika ingin user tidak perlu install apa-apa, ganti jadi `true` (ukuran installer jadi besar, ~100MB+).
*   `-o ./publish`: Hasilnya akan disimpan di folder `publish`.

## 3. Membuat Installer dengan Inno Setup

Untuk membuat `Setup_DesktopMusicPlayer.exe` (seperti installer aplikasi profesional), kita gunakan **Inno Setup** (Gratis).

### Langkah-langkah:
1.  **Download & Install Inno Setup**: [https://jrsoftware.org/isdl.php](https://jrsoftware.org/isdl.php)
2.  **Jalankan Script Installer**:
    Saya sudah membuatkan file `setup.iss` di folder project ini.
    *   Klik kanan file `setup.iss` -> Open With -> Inno Setup Compiler.
    *   Klik tombol **Run** (Play button) atau Build.
    *   Inno Setup akan memaketkan semua file di folder `publish` menjadi satu file `.exe` installer.

## 4. Distribusi
File hasil di folder `InstallerOutput` adalah file yang Anda berikan ke pengguna. Mereka tinggal double-click, Next-Next-Finish, dan aplikasi terinstall!

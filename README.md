# SmartScale — Industrial Weighing Dashboard
### Departemen MPD - Reject Non Proses | PT Riau Sakti United Plantations

![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![Language](https://img.shields.io/badge/Language-C%23-purple)
![Framework](https://img.shields.io/badge/Framework-.NET%20WinForms-blueviolet)
![Protocol](https://img.shields.io/badge/Protocol-MQTT-orange)
![Hardware](https://img.shields.io/badge/Hardware-Arduino-teal)
![Database](https://img.shields.io/badge/Database-SQLite-lightgrey)

---

## 📋 Deskripsi

SmartScale MPD-RNP adalah aplikasi desktop berbasis **C# WinForms** untuk sistem timbang industri di departemen **MPD - Reject Non Proses**. Aplikasi ini mengendalikan gate fisik melalui **Arduino** secara otomatis maupun manual, membaca berat dari timbangan digital via Serial Port, dan mengirim data ke server pusat via **MQTT**.

---

## ✨ Fitur Utama

- **Kontrol Gate via Arduino** — Trigger buka/tutup gate fisik langsung dari dashboard
- **Mode Auto** — Gate terbuka otomatis saat berat mencapai setpoint, tutup otomatis saat timbangan kosong
- **Mode Manual** — Operator kontrol penuh, gate bisa dibuka/ditutup kapan saja lewat tombol di dashboard
- **Pembacaan Berat Real-time** — Baca data timbangan via Serial Port
- **Kirim Data via MQTT** — Data berat, shift, dan waktu dikirim ke server EMQX
- **Offline Retry (SQLite)** — Data tersimpan lokal saat jaringan putus, dikirim ulang dengan timestamp asli saat koneksi pulih
- **Multi-Shift** — Support Shift 1, 2, dan 3
- **Activity Log** — Log real-time setiap aktivitas gate dan pengiriman data

---

## 🛠️ Tech Stack

| Komponen | Teknologi |
|----------|-----------|
| UI Framework | C# WinForms (.NET) |
| Komunikasi Timbangan | SerialPort (RS232) |
| Kontrol Gate | SerialPort → Arduino |
| Pengiriman Data | MQTT (MQTTnet) via EMQX Broker |
| Penyimpanan Offline | SQLite |
| Serialisasi Data | Newtonsoft.Json |

---

## 🏗️ Arsitektur Sistem

```
Timbangan Digital
        │ Serial Port
        ▼
  SmartScale Dashboard (C# WinForms)
        │                        │
        │ MQTT                   │ Serial Port
        ▼                        ▼
  EMQX Broker             Arduino Uno/Nano
        │                        │
        ▼                        ▼
  Server / Database         Gate Fisik
                          (OPEN / CLOSE)
```

---

## ⚙️ Alur Kerja

### Mode Auto
```
Berat terbaca ≥ Setpoint
        ↓
Trigger → Arduino → Gate OPEN
Data dikirim via MQTT
        ↓
Berat turun ≤ Threshold (timbangan kosong)
        ↓
Trigger → Arduino → Gate CLOSE
Siap siklus berikutnya
```

### Mode Manual
```
Operator klik tombol OPEN di dashboard
        ↓
Trigger → Arduino → Gate OPEN
        ↓
Operator klik tombol CLOSE di dashboard
        ↓
Trigger → Arduino → Gate CLOSE
        ↓
Operator klik Kirim → Data dikirim via MQTT
```

### Offline Retry
```
Data dikirim → MQTT gagal (jaringan putus)
        ↓
Data + timestamp asli disimpan ke SQLite
        ↓
Jaringan pulih → Data dikirim ulang
        ↓
Server menerima data dengan timestamp kejadian yang benar
```

---

## 📡 Format MQTT Payload

```json
{
  "shift": 1,
  "berat": "150 kg",
  "waktu": "2026-05-30 08:15:32",
  "kondisi": "Reject Non Proses"
}
```

**Topic:** `smartscale/mpd-rnp`

---

## 🔌 Koneksi Hardware

| Perangkat | Koneksi | Keterangan |
|-----------|---------|------------|
| Timbangan Digital | COM Port (RS232) | Baca data berat |
| Arduino | COM Port (USB) | Kontrol gate OPEN/CLOSE |
| EMQX Broker | TCP `192.168.12.226:1883` | Kirim data ke server |

### Perintah Arduino
```
OPEN  → Buka gate
CLOSE → Tutup gate
```

---

## 🚀 Cara Menjalankan

### Prasyarat
- Windows 10/11
- .NET Framework 4.7.2 atau lebih baru
- EMQX MQTT Broker (default: `192.168.12.226:1883`)
- Arduino terhubung via USB
- Timbangan digital terhubung via Serial Port

### Langkah
1. Clone repo ini
2. Buka `SmartScale.sln` di Visual Studio
3. Build solution (`Ctrl+Shift+B`)
4. Jalankan aplikasi
5. Pilih COM Port timbangan dan Arduino Port
6. Klik **Connect**
7. Pilih **Shift** aktif
8. Pilih mode **Auto** atau **Manual**
9. Untuk Manual — gunakan tombol **OPEN/CLOSE** untuk kontrol gate

---

## 📁 Struktur Project

```
SmartScale/
├── SmartScale/
│   ├── frmTimbang.cs         ← Form utama & logika utama
│   ├── frmTimbang.Designer.cs
│   ├── SQLiteHelper.cs       ← Helper database SQLite
│   └── ...
├── packages/                 ← NuGet packages
└── SmartScale.sln
```

---

## 👤 Developer

**Irvana Dimas Saputra**
Automation Engineer — IT Department
PT Riau Sakti United Plantations (Sambu Group)

---

## 📄 Lisensi

Internal use only — PT Riau Sakti United Plantations

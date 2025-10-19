# 🎓 Rota A Teacher Pack — Kullanım Kılavuzu

## ✅ Aktif Özellikler

### 1️⃣ **Instructions (Ana Kurallar)** — Otomatik Aktif ✨

`.github/copilot-instructions.md` dosyası **tüm chat etkileşimlerinde** otomatik olarak kullanılır.

**Kontrol:**

- Chat'i aç (Ctrl+Shift+I)
- Ayarlar (⚙️) → **Instructions** menüsüne bak
- Dosyayı görebilir ve açıp/kapatabilirsin

### 2️⃣ **Prompt Files (Komut Şablonları)** ✅

Chat'te **`#`** yazınca menüden seçebilirsin:

- **`#teach-me-term`** → Bir terimi öğret (tanım + örnek + benzetme + mini sınav)
- **`#explain-line-by-line`** → Seçili kodu satır satır açıkla
- **`#step-plan`** → İşi 3 mikro adıma böl ve her adımda onay iste
- **`#endpoint-plan`** → Yeni endpoint tasarla (istek/yanıt/test)

**Nasıl Kullanılır:**

1. Chat'te `#` yaz
2. Menüden prompt seç
3. İstenirse parametre gir (örn: terim adı)

## 🎯 "Mod" Olmadan Nasıl Kullanılır?

**Instructions dosyası zaten her zaman aktif!** Yani:

- Her chat sorusunda **Rota A kuralları** uygulanır
- Küçük adımlar, satır satır açıklama otomatik gelir
- Manuel "mod seçme" gerekmez

### İstersen Manual Hatırlatma:

Chat'te şunu yazabilirsin:

```
Lütfen Rota A kurallarına göre hareket et:
- Küçük adımlar
- Satır satır açıklama
- Onay iste
```

## 📝 Test Et

### Test 1: Instructions çalışıyor mu?

Chat'te sor:

```
Next.js'te bir todo listesi sayfası oluştur
```

**Beklenen:** AI küçük adımlar halinde, onay isteyerek ilerlemeli.

### Test 2: Prompt Files çalışıyor mu?

Chat'te yaz:

```
#teach-me-term
```

Terim: `DTO`

**Beklenen:** Tanım + örnek + benzetme + mini sınav

### Test 3: Seçili kod açıklama

Bir kod bloğu seç → Chat'te:

```
#explain-line-by-line
```

**Beklenen:** Satır satır açıklama + hata noktaları + kontrol soruları

---

## 🆘 Sorun Giderme

### "Instructions görünmüyor"

1. VS Code'u yeniden yükle (Ctrl+Shift+P → `Developer: Reload Window`)
2. Chat → ⚙️ → Instructions → `.github/copilot-instructions.md` aktif mi kontrol et

### "Prompt'lar menüde yok"

1. Chat'te **`#`** yazdığından emin ol (boşluk olmadan)
2. `.github/prompts/*.prompt.md` dosyaları var mı kontrol et
3. `.vscode/settings.json` → `chat.promptFiles.enabled: true` olmalı

### "AI kuralları uygulamıyor"

1. Instructions'ı manual aç/kapat (⚙️ menüsünden)
2. Chat'in başında "Lütfen .github/copilot-instructions.md dosyasını oku" yaz

---

## 📚 Dosya Yapısı

```
borsaGPT-proje_1/
├── .github/
│   ├── copilot-instructions.md    # Ana kurallar (otomatik aktif)
│   └── prompts/                    # Prompt şablonları
│       ├── teach-me-term.prompt.md
│       ├── explain-line-by-line.prompt.md
│       ├── step-plan.prompt.md
│       └── endpoint-plan.prompt.md
├── .vscode/
│   ├── settings.json               # Copilot ayarları
│   └── extensions.json             # Önerilen eklentiler
└── README-Teacher-Pack.md          # Kurulum kılavuzu
```

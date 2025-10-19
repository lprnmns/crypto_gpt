# Rota A — Copilot Teacher Pack

Bu paket, VS Code Copilot Chat'i **öğretici moda** alır ve yalnızca **Rota A** teknolojilerini kullanacak şekilde sınırlar.

## Kurulum

1. Bu klasörü **proje köküne** çıkarın (`.github` ve `.vscode` klasörleri oluşacak).
2. VS Code'u yeniden yükleyin: **Ctrl+Shift+P** → `Developer: Reload Window`
3. Chat panelinde **ayarlar ikonuna** (⚙️) tıklayın

## Kullanım

### 📋 Instructions (Ana Kurallar)

- **Otomatik aktif:** `.github/copilot-instructions.md` dosyası tüm chat yanıtlarında kullanılır
- Chat → ⚙️ → **Instructions** menüsünden görebilirsin

### 📝 Prompt Files (Komut Şablonları)

Chat'te **`#`** yazınca açılan menüden seçebilirsin:

- `#teach-me-term` → Bir terimi öğret (tanım + örnek + benzetme + mini sınav)
- `#explain-line-by-line` → Seçili kodu satır satır açıkla
- `#step-plan` → 3 mikro adıma böl ve her adımda onay iste
- `#endpoint-plan` → Yeni endpoint tasarla (istek/yanıt/test)

### 🎓 Modes (Öğretici Mod)

- Chat → ⚙️ → **Modes** → **"🎓 Rota A — Öğretici Mod"**
- Bu mod aktifken AI:
  - Yalnızca Rota A teknolojilerini kullanır
  - Küçük adımlar halinde ilerler
  - Her adımı satır satır açıklar
  - Büyük değişikliklerde önce plan gösterir

## Dosya Yapısı

```
.github/
├── copilot-instructions.md           # Ana kurallar (Instructions)
└── prompts/                           # Prompt şablonları
    ├── teach-me-term.prompt.md
    ├── explain-line-by-line.prompt.md
    ├── step-plan.prompt.md
    └── endpoint-plan.prompt.md

.vscode/
├── settings.json                      # Copilot ayarları + Modes tanımı
└── extensions.json                    # Önerilen eklentiler
```

> Kuralları düzenlemek için `.github/copilot-instructions.md` dosyasını değiştirebilirsin.

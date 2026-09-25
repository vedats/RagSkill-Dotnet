# RagSkill-Dotnet — .NET'te "belgelerinle sohbet" uygulamaları için Claude Code skill'i

**[English](README.md)**

.NET 10 Blazor Server ile **çalışan bir RAG (erişimle güçlendirilmiş üretim) sohbet uygulaması** kuran ya da
bunu mevcut ASP.NET Core / Blazor projene ekleyen bir [Claude Code](https://claude.com/claude-code) skill'i.
Kullanıcılar PDF/Markdown dosyaları yükleyip soru sorar; cevaplar tıklanabilir kaynak gösterimiyle gelir.

Yalnızca tavsiye veren RAG skill'lerinden farkı: **test edilmiş, çalışan bir şablon**, scriptler ve bu teknoloji
yığınında gerçekten yaşanmış hataların çözümleriyle geliyor. Claude zaten çalışan koddan başlıyor.

## Oluşturulan uygulamanın özellikleri

- 📄 **Belgeler sayfası** — yükleme (PDF, Markdown; boyut/adet sınırı, dosya bazında durum) ve onaylı silme
- 💬 **Kaynaklı cevaplar** — cevap, dosya adını ve alıntıyı gösterir; tıklayınca PDF/Markdown görüntüleyici alıntıda açılır
- ⚡ **Hızlı yeniden başlatma** — yalnızca yeni/değişen dosyalar indekslenir, silinen dosyalar indeksten çıkarılır, indeksleme açılışta arka planda başlar
- 🧠 **Hafıza** — sohbet tarayıcı başına kaydedilir; sayfa değişince, yenilenince veya uygulama yeniden başlayınca geri gelir; modele yalnızca son N mesaj gider
- 🛡️ **Dayanıklı** — kararsız embedding çağrıları tekrar denenir, Ollama kapalıysa çökme yerine uyarı gösterilir, Ollama açılınca başarısız belgeler otomatik indekslenir
- 🔌 **Yerel öncelikli** — embedding yerelde (`all-minilm`); sohbet Ollama üzerinden bulut (`gpt-oss:120b-cloud`) veya yerel modelle

## Gereksinimler

- [Claude Code](https://claude.com/claude-code), [.NET 10 SDK](https://dotnet.microsoft.com/download), Python 3.9+
- Çalışan [Ollama](https://ollama.com); `*-cloud` modeller için Ollama uygulamasında oturum açık olmalı

## Kurulum

**Eklenti olarak (önerilen):**

```bash
claude plugin marketplace add vedats/RagSkill-Dotnet
claude plugin install dotnet-rag-chat@dotnet-rag-chat
```

**Elle:** `skills/dotnet-rag-chat/` klasörünü `~/.claude/skills/dotnet-rag-chat/` altına kopyala (tüm projeler için)
veya `<proje>/.claude/skills/dotnet-rag-chat/` altına (tek proje için).

## Kullanım

Ne istediğini yazman yeterli:

- *"Ürün kılavuzlarımızla (PDF) sohbet eden bir Blazor uygulaması oluştur, Ollama kullan."*
- *"./ShopAdmin projeme kaynak gösteren belge soru-cevap özelliği ekle."*
- *"RAG uygulamam her açılışta tüm PDF'leri yeniden indeksliyor, ilk soru dakikalar sürüyor."*

Scriptleri Claude olmadan da çalıştırabilirsin:

```bash
python skills/dotnet-rag-chat/scripts/scaffold.py BelgeSohbet --output ./BelgeSohbet
cd BelgeSohbet && ollama pull all-minilm && dotnet run

python skills/dotnet-rag-chat/scripts/integrate.py ./ShopAdmin   # ardından references/integrate-existing.md
```

## Sınırlamalar

- Oluşturulan uygulamada kimlik doğrulama yok; internete açmadan önce ekle.
- Üç paket önizleme sürümünde (`CommunityToolkit.VectorData.SqliteVec`, `Microsoft.Extensions.DataIngestion*`); sürümleri sabitle.
- Hazır olarak yalnızca Ollama destekleniyor; diğer sağlayıcılar `Program.cs`'te küçük bir değişiklik ister.

## Lisans

[MIT](LICENSE). Paketlenen üçüncü taraf kodu kendi lisansını korur — bkz. [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

import sys
import json
import PyPDF2
import os
from collections import Counter
import nltk
from nltk.corpus import stopwords
import string
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

# NLTK ve stopwords indir
nltk.download("stopwords")
nltk.download("punkt")

def pdf_to_text(pdf_path):
    """PDF'yi metne çevirir."""
    try:
        with open(pdf_path, "rb") as file:
            reader = PyPDF2.PdfReader(file)
            text = "\n".join([page.extract_text() for page in reader.pages if page.extract_text()])
        return text
    except Exception as e:
        return f"Hata: {str(e)}"

def ngram_olustur(kelimeler, n):
    """Manuel olarak n-gram (bigram, trigram) listesi oluşturur."""
    return [" ".join(kelimeler[i:i+n]) for i in range(len(kelimeler)-n+1) if len(kelimeler) >= n]

def anahtar_kelime_cikar(metin):
    """Metinden en sık geçen anlamlı anahtar kelimeleri çıkarır."""
    stop_words = set(stopwords.words("english"))  # İngilizce stopwords listesini kullan
    kelimeler = metin.lower().split()

    # Noktalama işaretlerini ve gereksiz kelimeleri filtrele
    temiz_kelime_listesi = [
        kelime.strip(string.punctuation)
        for kelime in kelimeler
        if kelime not in stop_words and kelime.isalpha()  # Sadece harflerden oluşan kelimeleri al
    ]

    # En sık geçen anlamlı kelimeleri belirle
    kelime_sayaci = Counter(temiz_kelime_listesi)
    tekil_kelime_listesi = [kelime for kelime, _ in kelime_sayaci.most_common(10)]

    # Bigram ve trigram terimleri manuel oluşturalım
    bigram_listesi = ngram_olustur(temiz_kelime_listesi, 2)
    trigram_listesi = ngram_olustur(temiz_kelime_listesi, 3)

    bigram_sayaci = Counter(bigram_listesi)
    trigram_sayaci = Counter(trigram_listesi)

    en_fazla_gecen_bigramlar = [bigram for bigram, _ in bigram_sayaci.most_common(5)]
    en_fazla_gecen_trigramlar = [trigram for trigram, _ in trigram_sayaci.most_common(5)]

    # Hem tekil kelimeler, hem bigram, hem de trigramları döndür
    return tekil_kelime_listesi + en_fazla_gecen_bigramlar + en_fazla_gecen_trigramlar

def alan_atama(anahtar_kelimeler):
    """Anahtar kelimelere göre en uygun ana alanı belirler ve sadece o alanın en iyi eşleşmelerini gösterir."""
    alanlar = {
        "Artificial Intelligence and Machine Learning": {
            "deep learning": "Neural Networks, Backpropagation, CNN, RNN, Transformer Models, Autoencoders, LSTM Model",
            "natural language processing": "Tokenization, Sentiment Analysis, Named Entity Recognition (NER), Word Embeddings, Text Classification",
            "computer vision": "Image Recognition, Object Detection, Feature Extraction, Convolutional Neural Networks (CNN), Image Segmentation",
            "generative ai": "GANs (Generative Adversarial Networks), Variational Autoencoders (VAE), Diffusion Models, Text-to-Image, Large Language Models (LLM)"
        },
        "Cybersecurity": {
            "network security": "Firewalls, Intrusion Detection Systems (IDS), Virtual Private Networks (VPN), Zero Trust Security, Threat Intelligence",
            "encryption algorithms": "AES, RSA, ECC (Elliptic Curve Cryptography), Hashing (SHA-256, MD5), Symmetric & Asymmetric Encryption",
            "authentication systems": "Multi-Factor Authentication (MFA), Biometric Authentication, OAuth, SAML, Password Hashing",
            "secure software development": "Secure Coding Practices, OWASP Top 10, Code Review, Static and Dynamic Analysis, DevSecOps"
        },
        "Big Data and Data Analytics": {
            "data mining": "Association Rule Learning, Clustering (K-Means, DBSCAN), Decision Trees, Feature Engineering",
            "time series analysis": "ARIMA, Exponential Smoothing, Seasonal Decomposition, Anomaly Detection, Forecasting Models",
            "data visualization": "Dashboarding, Business Intelligence (BI), Matplotlib, Tableau, Power BI, D3.js",
            "data processing systems": "MapReduce, Distributed Computing, Apache Hive, Apache Flink, In-Memory Processing"
        }
    }

    # Eşleşen başlıkları ve ana alanları sakla
    eslesen_basliklar = {}
    ana_alan_eslesme_sayilari = {}

    for alan, alt_basliklar in alanlar.items():
        for baslik, aciklama in alt_basliklar.items():
            eslesme_sayisi = sum(1 for kelime in anahtar_kelimeler if kelime == baslik or kelime.lower() in aciklama.lower())
            if eslesme_sayisi > 0:
                eslesen_basliklar[baslik] = (eslesme_sayisi, alan)
                if alan in ana_alan_eslesme_sayilari:
                    ana_alan_eslesme_sayilari[alan] += eslesme_sayisi
                else:
                    ana_alan_eslesme_sayilari[alan] = eslesme_sayisi

    # En iyi eşleşen ana alanı bul (en çok eşleşme alan)
    if ana_alan_eslesme_sayilari:
        en_iyi_alan = max(ana_alan_eslesme_sayilari, key=ana_alan_eslesme_sayilari.get)
    else:
        return [], "Bilinmeyen"

    # En iyi ana alana ait başlıkları seç ve en çok eşleşenden en aza sırala
    en_iyi_basliklar = [b for b, (s, a) in sorted(eslesen_basliklar.items(), key=lambda x: x[1][0], reverse=True) if a == en_iyi_alan]

    return en_iyi_basliklar, en_iyi_alan


if __name__ == "__main__":
    pdf_path = sys.argv[1]
    
    if not os.path.exists(pdf_path):
        print(json.dumps({"hata": f"PDF dosyası bulunamadı: {pdf_path}"}))
        sys.exit(1)

    metin = pdf_to_text(pdf_path)
    if metin.startswith("Hata:"):
        print(json.dumps({"hata": metin}))
        sys.exit(1)

    anahtar_kelimeler = anahtar_kelime_cikar(metin)
    en_iyi_basliklar, en_iyi_alan = alan_atama(anahtar_kelimeler)

    # **C# ile uyumlu JSON çıktısı**
    sonuc = {
        "belirlenen_ana_alan": en_iyi_alan,
        "eşleşen_alt_başlıklar": en_iyi_basliklar
    }

    print(json.dumps(sonuc))


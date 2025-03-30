import io
import fitz  # PyMuPDF
import re
import spacy
from spacy.matcher import Matcher, PhraseMatcher
import logging

from Crypto.Cipher import AES
from Crypto.Util.Padding import pad, unpad
import base64

import numpy as np
import cv2
from PIL import Image

# AES sabitleri
AES_KEY = b'ThisIsASecretKey'  # 16 byte key

def aes_encrypt(plain_text):
    cipher = AES.new(AES_KEY, AES.MODE_ECB)
    padded = pad(plain_text.encode(), AES.block_size)
    encrypted = cipher.encrypt(padded)
    return base64.b64encode(encrypted).decode()

def aes_decrypt(cipher_text_b64):
    cipher = AES.new(AES_KEY, AES.MODE_ECB)
    encrypted = base64.b64decode(cipher_text_b64)
    decrypted = unpad(cipher.decrypt(encrypted), AES.block_size)
    return decrypted.decode()

# Log ayarı
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger('AnonService.ner')

class PdfNerProcessor:
    def __init__(self):
        try:
            try:
                self.nlp = spacy.load("tr_core_news_trf")
                logger.info("TRF tabanlı Türkçe NER modeli yüklendi.")
            except:
                self.nlp = spacy.load("en_core_web_trf")
                logger.info("TRF tabanlı İngilizce NER modeli yüklendi.")
        except:
            self.nlp = spacy.blank("en")
            logger.warning("Dil modeli bulunamadı, boş model yüklendi.")

        self.matcher = Matcher(self.nlp.vocab)
        self.phrase_matcher = PhraseMatcher(self.nlp.vocab)

        email_pattern = [{"TEXT": {"REGEX": r"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}"}}]
        self.matcher.add("EMAIL", [email_pattern])
        self.original_faces = {}  # Sayfa numarasına göre orijinal yüzleri saklayacağız

    def draw_text_like_original(self, page, rect, text, font_size):
        x = rect.x0
        y = rect.y1 - 1
        page.draw_rect(rect, color=(1, 1, 1), fill=(1, 1, 1))
        page.insert_text((x, y), text, fontsize=font_size, color=(0, 0, 0))

    def extract_text_from_pdf(self, pdf_bytes):
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        full_text = ""
        for page in doc:
            full_text += page.get_text()
        doc.close()
        return full_text

    def detect_entities(self, text):
        if not text or not self.nlp:
            return []

        blacklist_keywords = [
            "data", "model", "response", "memory", "recognition", "machine", "forest", "tree",
            "apnea", "vector", "knn", "random", "interaction", "abstract", "method", "results",
            "introduction", "explore", "review", "withdrawal", "spectrum", "decision",
            "valence", "arousal", "circular", "tellegen", "restrictions", "rate", "signal"
        ]

        entities = []
        doc = self.nlp(text)

        for ent in doc.ents:
            ent_text = ent.text.strip()
            ent_label = ent.label_
            lower_text = ent_text.lower()

            if any(bad_word in lower_text for bad_word in blacklist_keywords):
                continue

            if ent_label in ["PERSON", "PER"]:
                if len(ent_text.split()) < 2 or len(ent_text) < 5:
                    continue
                if "et al" in lower_text:
                    continue

            if ent_label in ["ORG", "INSTITUTION"]:
                if len(ent_text) < 6 or lower_text.count(" ") > 7 or len(ent_text.split()) < 2:
                    continue
                if any(word in lower_text for word in blacklist_keywords):
                    continue

            if ent_label in ["GPE", "LOC"]:
                continue

            entities.append((ent.start_char, ent.end_char, ent_label, ent_text))

        matcher_entities = self.matcher(doc)
        for match_id, start, end in matcher_entities:
            match_label = self.nlp.vocab.strings[match_id]
            span_text = doc[start:end].text
            entities.append((doc[start].idx, doc[end - 1].idx + len(doc[end - 1]), match_label, span_text))

        phrase_matches = self.phrase_matcher(doc)
        for match_id, start, end in phrase_matches:
            match_label = self.nlp.vocab.strings[match_id]
            span_text = doc[start:end].text
            entities.append((doc[start].idx, doc[end - 1].idx + len(doc[end - 1]), match_label, span_text))

        email_regex = r'[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}'
        for match in re.finditer(email_regex, text):
            entities.append((match.start(), match.end(), "EMAIL", match.group()))

        return entities

    def detect_entities_from_first_page(self, pdf_bytes):
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        if len(doc) == 0:
            return []
        first_page_text = doc[0].get_text()
        doc.close()
        return self.detect_entities(first_page_text)

    def process_pdf(self, pdf_bytes, selected_entity_labels=None):
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        output = io.BytesIO()

        if selected_entity_labels is None:
            selected_entity_labels = ["PERSON", "EMAIL", "PHONE", "INSTITUTION", "ORG", "TURKISH_UNIVERSITY"]

        entities = self.detect_entities_from_first_page(pdf_bytes)

        value_to_mask = {}
        mask_to_encrypted = {}
        counters = {"PERSON": 1, "EMAIL": 1, "ORG": 1, "INSTITUTION": 1}

        for _, _, typ, val in entities:
            val = val.strip()
            if typ in selected_entity_labels:
                if val not in value_to_mask:
                    label = "KISI" if typ == "PERSON" else "EPOSTA" if typ == "EMAIL" else "KURUM"
                    counter = counters.get(typ, 1)
                    mask = f"[{label}{counter}]"
                    counters[typ] = counter + 1
                    value_to_mask[val] = mask
                    mask_to_encrypted[mask] = aes_encrypt(val)

        self.mask_to_encrypted = mask_to_encrypted

        for page in doc:
            for val, mask in value_to_mask.items():
                try:
                    rects = page.search_for(val)
                    for rect in rects:
                        font_size = 0
                        spans = page.get_text("dict")["blocks"]
                        for block in spans:
                            for line in block.get("lines", []):
                                for span in line.get("spans", []):
                                    if val in span["text"]:
                                        font_size = span.get("size", 5)
                                        break
                        if font_size == 0:
                            font_size = 5
                        self.draw_text_like_original(page, rect, mask, font_size)
                except Exception as e:
                    logger.warning(f"{val} için maskeleme hatası: {e}")

        doc.save(output)
        doc.close()

        result_pdf = output.getvalue()
        result_pdf = self.blur_faces_in_pdf(result_pdf)
        return result_pdf

    def blur_faces_in_pdf(self, pdf_bytes):
        doc = fitz.open("pdf", pdf_bytes)
        face_cascade = cv2.CascadeClassifier(cv2.data.haarcascades + "haarcascade_frontalface_default.xml")

        for page in doc:
            pix = page.get_pixmap(dpi=150)
            img = Image.frombytes("RGB", [pix.width, pix.height], pix.samples)
            img_cv = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)

            gray = cv2.cvtColor(img_cv, cv2.COLOR_BGR2GRAY)
            faces = face_cascade.detectMultiScale(gray, 1.1, 4)

            for (x, y, w, h) in faces:
                if page.number not in self.original_faces:
                    self.original_faces[page.number] = []
                self.original_faces[page.number].append((x, y, w, h, img_cv[y:y+h, x:x+w].copy()))

                face = img_cv[y:y+h, x:x+w]
                face = cv2.GaussianBlur(face, (99, 99), 30)
                img_cv[y:y+h, x:x+w] = face

            final_img = Image.fromarray(cv2.cvtColor(img_cv, cv2.COLOR_BGR2RGB)).convert("RGB")
            img_buffer = io.BytesIO()
            final_img.save(img_buffer, format="PNG")
            img_data = img_buffer.getvalue()

            # ✅ Direkt sayfanın üzerine bindirme (metinleri korur)
            page.insert_image(page.rect, stream=img_data, overlay=True)

        output = io.BytesIO()
        doc.save(output)
        doc.close()
        return output.getvalue()


    def restore_faces_in_pdf(self, pdf_bytes):
        if not hasattr(self, 'original_faces') or not self.original_faces:
            logger.info("Orijinal yüz verisi yok, blur geri alınamadı.")
            return pdf_bytes

        doc = fitz.open("pdf", pdf_bytes)

        for page in doc:
            page_num = page.number
            pix = page.get_pixmap(dpi=150)
            img = Image.frombytes("RGB", [pix.width, pix.height], pix.samples)
            img_cv = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)

            if page_num in self.original_faces:
                for (x, y, w, h, original_face) in self.original_faces[page_num]:
                    img_cv[y:y+h, x:x+w] = original_face

            final_img = Image.fromarray(cv2.cvtColor(img_cv, cv2.COLOR_BGR2RGB)).convert("RGB")
            img_buffer = io.BytesIO()
            final_img.save(img_buffer, format="PNG")
            img_data = img_buffer.getvalue()

            # ✅ Orijinal sayfa üzerine bindirme
            page.insert_image(page.rect, stream=img_data, overlay=True)

        output = io.BytesIO()
        doc.save(output)
        doc.close()
        return output.getvalue()


    def restore_pdf(self, pdf_bytes):
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        output = io.BytesIO()

        if not hasattr(self, 'mask_to_encrypted'):
            logger.warning("mask_to_encrypted bulunamadı.")
            return pdf_bytes

        for page in doc:
            for mask, encrypted in self.mask_to_encrypted.items():
                try:
                    original = aes_decrypt(encrypted)
                    rects = page.search_for(mask)
                    for rect in rects:
                        self.draw_text_like_original(page, rect, original, 5)
                except Exception as e:
                    logger.warning(f"{mask} geri çözüm hatası: {e}")

        doc.save(output)
        doc.close()

        result = output.getvalue()
        result = self.restore_faces_in_pdf(result)
        return result

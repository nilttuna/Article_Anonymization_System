from flask import Flask, request, jsonify, send_file
from flask_cors import CORS
from io import BytesIO
import base64
from PdfNerProcessor import PdfNerProcessor

app = Flask(__name__)
CORS(app)

processor = PdfNerProcessor()

@app.route('/detect', methods=['POST'])
def detect_entities():
    if 'pdf' not in request.files:
        return jsonify({'error': 'PDF dosyası bulunamadı'}), 400

    pdf_file = request.files['pdf']
    pdf_bytes = pdf_file.read()

    entities = processor.detect_entities_from_first_page(pdf_bytes)

    result = []
    for start, end, ent_type, ent_text in entities:
        result.append({
            "type": ent_type,
            "text": ent_text
        })

    return jsonify(result), 200

@app.route('/anonymize', methods=['POST'])
def anonymize_pdf():
    if 'pdf' not in request.files:
        return jsonify({'error': 'PDF dosyası eksik'}), 400

    types_to_anonymize = request.form.getlist('types')
    pdf_file = request.files['pdf']
    pdf_bytes = pdf_file.read()

    # Yeni versiyon: doğrudan tür listesiyle çalışıyoruz
    processed_pdf = processor.process_pdf(pdf_bytes, selected_entity_labels=types_to_anonymize)
    return send_file(BytesIO(processed_pdf), mimetype='application/pdf')

@app.route('/restore', methods=['POST'])
def restore_pdf_api():
    if 'pdf' not in request.files:
        return jsonify({'error': 'PDF dosyası eksik'}), 400

    pdf_bytes = request.files['pdf'].read()
    restored_pdf = processor.restore_pdf(pdf_bytes)

    return send_file(BytesIO(restored_pdf), mimetype='application/pdf')


if __name__ == '__main__':
    app.run(debug=True, port=5000)

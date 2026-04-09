// ── DocXAI Frontend ──────────────────────────────────────────────────────────
(function () {
    'use strict';

    // ── DOM refs ─────────────────────────────────────────────────────────────
    const dropZone        = document.getElementById('drop-zone');
    const fileInput       = document.getElementById('file-input');
    const fileInfo        = document.getElementById('file-info');
    const fileNameEl      = document.getElementById('file-name');
    const fileSizeEl      = document.getElementById('file-size');
    const removeFileBtn   = document.getElementById('remove-file');
    const uploadBtn       = document.getElementById('upload-btn');
    const btnLabel        = uploadBtn.querySelector('.btn-label');
    const btnSpinner      = uploadBtn.querySelector('.btn-spinner');
    const progressSection = document.getElementById('progress-section');
    const progressBar     = document.getElementById('progress-bar');
    const progressText    = document.getElementById('progress-text');
    const errorSection    = document.getElementById('error-section');
    const errorText       = document.getElementById('error-text');
    const uploadSection   = document.getElementById('upload-section');
    const resultSection   = document.getElementById('result-section');
    const resultMeta      = document.getElementById('result-meta');
    const documentViewer  = document.getElementById('document-viewer');
    const copyBtn         = document.getElementById('copy-btn');
    const newUploadBtn    = document.getElementById('new-upload-btn');

    let selectedFile = null;
    let rawSpecContent = '';

    // ── Helpers ──────────────────────────────────────────────────────────────
    function formatBytes(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    function showError(msg) {
        errorText.textContent = msg;
        errorSection.classList.remove('hidden');
    }

    function hideError() {
        errorSection.classList.add('hidden');
    }

    function setUploading(uploading) {
        uploadBtn.disabled = uploading;
        btnLabel.textContent = uploading ? 'Processing…' : 'Generate Functional Document';
        btnSpinner.classList.toggle('hidden', !uploading);
        progressSection.classList.toggle('hidden', !uploading);
    }

    function setProgress(pct, text) {
        progressBar.style.width = pct + '%';
        if (text) progressText.textContent = text;
    }

    // ── File selection ──────────────────────────────────────────────────────
    function selectFile(file) {
        if (!file) return;
        selectedFile = file;
        fileNameEl.textContent = file.name;
        fileSizeEl.textContent = formatBytes(file.size);
        fileInfo.classList.remove('hidden');
        uploadBtn.disabled = false;
        hideError();
    }

    function clearFile() {
        selectedFile = null;
        fileInput.value = '';
        fileInfo.classList.add('hidden');
        uploadBtn.disabled = true;
    }

    // Click to browse
    dropZone.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => {
        if (fileInput.files.length > 0) selectFile(fileInput.files[0]);
    });

    // Remove selected file
    removeFileBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        clearFile();
    });

    // Drag & Drop
    ['dragenter', 'dragover'].forEach(evt =>
        dropZone.addEventListener(evt, (e) => {
            e.preventDefault();
            dropZone.classList.add('drag-over');
        })
    );
    ['dragleave', 'drop'].forEach(evt =>
        dropZone.addEventListener(evt, () => dropZone.classList.remove('drag-over'))
    );
    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        if (e.dataTransfer.files.length > 0) selectFile(e.dataTransfer.files[0]);
    });

    // ── Upload & Process ────────────────────────────────────────────────────
    uploadBtn.addEventListener('click', async () => {
        if (!selectedFile) return;

        hideError();
        setUploading(true);
        setProgress(10, 'Uploading file to Cloud Storage…');

        const formData = new FormData();
        formData.append('file', selectedFile);

        try {
            // Use XMLHttpRequest for upload progress tracking
            const result = await new Promise((resolve, reject) => {
                const xhr = new XMLHttpRequest();
                xhr.open('POST', '/api/orchestrator/upload');

                xhr.upload.addEventListener('progress', (e) => {
                    if (e.lengthComputable) {
                        const uploadPct = Math.round((e.loaded / e.total) * 40); // 0-40%
                        setProgress(uploadPct, 'Uploading file to Cloud Storage…');
                    }
                });

                xhr.upload.addEventListener('load', () => {
                    setProgress(45, 'File uploaded. AI is generating the functional specification…');

                    // Simulate progress steps while waiting for AI
                    const steps = [
                        { pct: 55, text: 'Reading design document…', delay: 2000 },
                        { pct: 65, text: 'Vertex AI (Gemini) is analysing the document…', delay: 5000 },
                        { pct: 75, text: 'Generating functional specification…', delay: 8000 },
                        { pct: 85, text: 'Creating .docx output…', delay: 12000 },
                        { pct: 90, text: 'Generating Playwright test scripts…', delay: 16000 },
                        { pct: 92, text: 'Triggering Cloud Build…', delay: 20000 },
                    ];
                    steps.forEach(s => {
                        setTimeout(() => {
                            if (progressBar.style.width !== '100%') {
                                setProgress(s.pct, s.text);
                            }
                        }, s.delay);
                    });
                });

                xhr.addEventListener('load', () => {
                    try {
                        const data = JSON.parse(xhr.responseText);
                        if (xhr.status >= 200 && xhr.status < 300) {
                            resolve(data);
                        } else {
                            reject(new Error(data.errorMessage || data.error || `Server returned ${xhr.status}`));
                        }
                    } catch {
                        reject(new Error('Invalid response from server.'));
                    }
                });

                xhr.addEventListener('error', () => reject(new Error('Network error during upload.')));
                xhr.addEventListener('timeout', () => reject(new Error('Upload timed out.')));
                xhr.timeout = 300000; // 5 minutes

                xhr.send(formData);
            });

            // Success
            setProgress(100, 'Done!');
            displayResult(result);

        } catch (err) {
            setUploading(false);
            showError(err.message || 'An unexpected error occurred.');
        }
    });

    // ── Display Result ──────────────────────────────────────────────────────
    function displayResult(result) {
        // Build metadata badges
        let metaHtml = '';
        if (result.correlationId) {
            metaHtml += `<span class="meta-badge">ID: ${escapeHtml(result.correlationId)}</span>`;
        }
        if (result.functionalSpecPath) {
            metaHtml += `<span class="meta-badge">📄 ${escapeHtml(result.functionalSpecPath)}</span>`;
        }
        if (result.testScriptPath) {
            metaHtml += `<span class="meta-badge">🧪 ${escapeHtml(result.testScriptPath)}</span>`;
        }
        if (result.buildJobId) {
            metaHtml += `<span class="meta-badge">🔨 Build: ${escapeHtml(result.buildJobId)}</span>`;
        }
        resultMeta.innerHTML = metaHtml;

        // Render functional spec content (markdown → HTML)
        rawSpecContent = result.functionalSpecContent || '';
        if (rawSpecContent) {
            documentViewer.innerHTML = marked.parse(rawSpecContent);
        } else {
            documentViewer.innerHTML =
                '<p style="color: var(--color-text-secondary);">The functional specification was generated and saved to Cloud Storage, but no preview content was returned.</p>';
        }

        // Show result, keep upload visible
        uploadSection.classList.add('hidden');
        resultSection.classList.remove('hidden');
    }

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // ── Copy to clipboard ───────────────────────────────────────────────────
    copyBtn.addEventListener('click', async () => {
        if (!rawSpecContent) return;
        try {
            await navigator.clipboard.writeText(rawSpecContent);
            const original = copyBtn.innerHTML;
            copyBtn.textContent = '✓ Copied';
            setTimeout(() => { copyBtn.innerHTML = original; }, 2000);
        } catch {
            // Fallback
            const ta = document.createElement('textarea');
            ta.value = rawSpecContent;
            document.body.appendChild(ta);
            ta.select();
            document.execCommand('copy');
            document.body.removeChild(ta);
        }
    });

    // ── New Upload ──────────────────────────────────────────────────────────
    newUploadBtn.addEventListener('click', () => {
        clearFile();
        setUploading(false);
        setProgress(0, '');
        hideError();
        resultSection.classList.add('hidden');
        uploadSection.classList.remove('hidden');
    });

})();

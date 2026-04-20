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
    const saveBtn         = document.getElementById('save-btn');
    const newUploadBtn    = document.getElementById('new-upload-btn');
    const genTestsBtn     = document.getElementById('gen-tests-btn');
    const viewTestsBtn    = document.getElementById('view-tests-btn');
    const testGenStatus   = document.getElementById('test-gen-status');
    const solutionNameInput = document.getElementById('solution-name');

    // Test Cases section
    const testcasesSection = document.getElementById('testcases-section');
    const tcCode           = document.getElementById('tc-code');
    const tcPre            = document.getElementById('tc-pre');
    const tcMeta           = document.getElementById('tc-meta');
    const tcBuildBanner    = document.getElementById('tc-build-banner');
    const tcLineCount      = document.getElementById('tc-line-count');
    const tcLoadError      = document.getElementById('tc-load-error');
    const tcLoadErrorMsg   = document.getElementById('tc-load-error-msg');
    const tcLoadErrorPath  = document.getElementById('tc-load-error-path');
    const tcRetryBtn       = document.getElementById('tc-retry-btn');
    const tcDownloadBtn    = document.getElementById('tc-download-btn');
    const tcCopyBtn        = document.getElementById('tc-copy-btn');
    const tcBackBtn        = document.getElementById('tc-back-btn');
    const tcNewUploadBtn   = document.getElementById('tc-new-upload-btn');

    let selectedFile    = null;
    let rawSpecContent  = '';
    let rawSpecPath     = '';
    let rawTestContent  = '';
    let rawTestPath     = '';

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

        const solutionName = solutionNameInput.value.trim();
        if (!solutionName) {
            showError('Please enter a Solution Name before uploading.');
            solutionNameInput.focus();
            return;
        }

        hideError();
        setUploading(true);
        setProgress(10, 'Uploading file to Cloud Storage…');

        const formData = new FormData();
        formData.append('file', selectedFile);
        formData.append('solutionName', solutionName);

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
                        { pct: 88, text: 'Finalising document…', delay: 12000 },
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

        // Surface Cloud Build warning if present
        if (result.buildWarning) {
            const warn = document.createElement('div');
            warn.className = 'build-warning';
            warn.innerHTML =
                `⚠️ <strong>Cloud Build skipped:</strong> ${escapeHtml(result.buildWarning)} ` +
                `<a href="https://console.developers.google.com/apis/api/cloudbuild.googleapis.com/overview" ` +
                `target="_blank" rel="noopener">Enable Cloud Build API →</a>`;
            resultMeta.after(warn);
        }

        // Render functional spec content (markdown → HTML)
        rawSpecContent = result.functionalSpecContent || '';
        rawSpecPath    = result.functionalSpecPath    || '';
        if (rawSpecContent) {
            documentViewer.innerHTML = marked.parse(rawSpecContent);
        } else {
            documentViewer.innerHTML =
                '<p style="color: var(--color-text-secondary);">The functional specification was generated and saved to Cloud Storage, but no preview content was returned.</p>';
        }

        // Show result, keep upload visible
        uploadSection.classList.add('hidden');
        resultSection.classList.remove('hidden');
        testGenStatus.classList.add('hidden');
    }

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // ── Save as Word (.docx) ──────────────────────────────────────────────
    saveBtn.addEventListener('click', async () => {
        if (!rawSpecContent) return;

        const originalHtml = saveBtn.innerHTML;
        saveBtn.disabled = true;
        saveBtn.textContent = 'Saving…';

        try {
            const solutionName = solutionNameInput.value.trim();
            const fileName = solutionName
                ? `${solutionName}-functional-specification`
                : 'functional-specification';

            const response = await fetch('/api/orchestrator/save-docx', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ content: rawSpecContent, fileName })
            });

            if (!response.ok) {
                const err = await response.json().catch(() => ({}));
                throw new Error(err.error || `Server error ${response.status}`);
            }

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName.endsWith('.docx') ? fileName : fileName + '.docx';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            saveBtn.innerHTML = '✓ Saved';
            setTimeout(() => {
                saveBtn.innerHTML = originalHtml;
                saveBtn.disabled = false;
            }, 2500);
        } catch (err) {
            saveBtn.innerHTML = originalHtml;
            saveBtn.disabled = false;
            showError('Could not save document: ' + (err.message || 'Unknown error'));
        }
    });

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

    // ── Generate Test Cases ─────────────────────────────────────────────────
    genTestsBtn.addEventListener('click', async () => {
        if (!rawSpecContent || !rawSpecPath) return;

        const genLabel   = genTestsBtn.querySelector('.btn-label');
        const genSpinner = genTestsBtn.querySelector('.btn-spinner');
        genTestsBtn.disabled = true;
        genLabel.textContent = 'Generating\u2026';
        genSpinner.classList.remove('hidden');
        testGenStatus.textContent = 'Calling Vertex AI to generate Playwright test scripts\u2026';
        testGenStatus.classList.remove('hidden');

        try {
            const response = await fetch('/api/orchestrator/generate-tests', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    functionalSpecContent: rawSpecContent,
                    functionalSpecPath: rawSpecPath
                })
            });

            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                throw new Error(data.errorMessage || data.error || `Server error ${response.status}`);
            }

            genLabel.textContent = 'Generate Test Cases';
            genTestsBtn.disabled = false;
            genSpinner.classList.add('hidden');
            testGenStatus.classList.add('hidden');

            displayTestCases(data);

        } catch (err) {
            testGenStatus.textContent = 'Error: ' + (err.message || 'Unknown error');
            genLabel.textContent = 'Generate Test Cases';
            genTestsBtn.disabled = false;
            genSpinner.classList.add('hidden');
        }
    });

    // ── Display Test Cases ──────────────────────────────────────────────────
    function displayTestCases(data) {
        rawTestContent = data.testScriptContent || '';
        rawTestPath    = data.testScriptPath    || '';

        // Build meta badges
        let metaHtml = '';
        if (rawTestPath) {
            metaHtml += `<span class="meta-badge">&#x1F9EA; ${escapeHtml(rawTestPath)}</span>`;
        }
        if (data.buildJobId) {
            metaHtml += `<span class="meta-badge">&#x1F528; Build: ${escapeHtml(data.buildJobId)}</span>`;
        }
        tcMeta.innerHTML = metaHtml;

        // Build warning banner
        if (data.buildWarning) {
            tcBuildBanner.innerHTML =
                `&#x26A0;&#xFE0F; <strong>Cloud Build skipped:</strong> ${escapeHtml(data.buildWarning)}`;
            tcBuildBanner.classList.remove('hidden');
        } else {
            tcBuildBanner.classList.add('hidden');
        }

        // Show the View Test Cases button in the spec panel for future navigation
        viewTestsBtn.classList.remove('hidden');

        // Navigate first so the section is visible before we render
        resultSection.classList.add('hidden');
        testcasesSection.classList.remove('hidden');
        hideTcError();
        window.scrollTo({ top: 0, behavior: 'smooth' });

        if (rawTestContent) {
            renderTestCode(rawTestContent);
        } else if (rawTestPath) {
            // Content was not in the API response — fetch from GCS fallback
            tcCode.textContent = '';
            tcLineCount.textContent = 'Loading…';
            fetchTestContent(rawTestPath).then(({ content, status }) => {
                if (content) {
                    renderTestCode(content);
                } else if (status === 'not-found') {
                    showTcError(
                        'The server needs to be restarted to load test content. ' +
                        'Click Back to Spec, restart the server, then click Generate Test Cases again.',
                        rawTestPath
                    );
                } else {
                    showTcError(
                        status || 'Could not retrieve content from Cloud Storage.',
                        rawTestPath
                    );
                }
            });
        } else {
            showTcError('No test content was returned by the server.', null);
        }
    }

    // ── Test Cases: Retry loading content ──────────────────────────────────
    tcRetryBtn.addEventListener('click', () => {
        if (!rawTestPath) return;
        hideTcError();
        tcLineCount.textContent = 'Loading…';
        fetchTestContent(rawTestPath).then(({ content, status }) => {
            if (content) {
                renderTestCode(content);
            } else if (status === 'not-found') {
                showTcError(
                    'Server endpoint still unavailable. Restart the server and click Generate Test Cases again.',
                    rawTestPath
                );
            } else {
                showTcError(
                    status || 'Could not retrieve content from Cloud Storage.',
                    rawTestPath
                );
            }
        });
    });

    // Fetch raw test-script text from GCS via the server endpoint.
    // Returns { content: string, status: null | string }
    //   status === null      → success
    //   status === 'not-found' → endpoint missing (server not restarted)
    //   status === <message>  → real error
    async function fetchTestContent(path) {
        try {
            const r = await fetch(`/api/orchestrator/test-content?path=${encodeURIComponent(path)}`);
            if (r.status === 404) {
                return { content: '', status: 'not-found' };
            }
            if (!r.ok) {
                const d = await r.json().catch(() => ({}));
                return { content: '', status: d.error || `Server error ${r.status}` };
            }
            const d = await r.json().catch(() => ({}));
            const content = d.content || '';
            if (content) rawTestContent = content;
            return { content, status: null };
        } catch (e) {
            return { content: '', status: e.message || 'Network error' };
        }
    }

    // Show / hide the error overlay inside the viewer
    function showTcError(msg, path) {
        tcLoadErrorMsg.textContent  = msg;
        tcLoadErrorPath.textContent = path || '';
        tcLoadErrorPath.style.display = path ? '' : 'none';
        tcLoadError.classList.remove('hidden');
        tcPre.classList.add('hidden');
        tcLineCount.textContent = '';
    }

    function hideTcError() {
        tcLoadError.classList.add('hidden');
        tcPre.classList.remove('hidden');
    }

    // Render code with syntax highlighting and line count
    function renderTestCode(rawCode) {
        const code = stripCodeFences(rawCode);
        tcCode.textContent = code || '// (empty file)';
        if (typeof hljs !== 'undefined') {
            hljs.highlightElement(tcCode);
        }
        const lines = code ? code.split('\n').length : 0;
        tcLineCount.textContent = lines > 0 ? `${lines} line${lines !== 1 ? 's' : ''}` : '';
    }

    function stripCodeFences(text) {
        if (!text) return '';
        return text
            .replace(/^```[\w]*\r?\n?/, '')
            .replace(/\r?\n?```\s*$/, '')
            .trim();
    }

    // ── Test Cases: Download ────────────────────────────────────────────────
    tcDownloadBtn.addEventListener('click', async () => {
        const originalHtml = tcDownloadBtn.innerHTML;

        // If content is not yet in memory, fetch from GCS first
        let content = rawTestContent;
        if (!content && rawTestPath) {
            tcDownloadBtn.disabled = true;
            tcDownloadBtn.textContent = 'Fetching…';
            const result = await fetchTestContent(rawTestPath);
            content = result.content;
            tcDownloadBtn.innerHTML = originalHtml;
            tcDownloadBtn.disabled = false;
            if (!content) {
                const msg = result.status === 'not-found'
                    ? 'Server needs restart — file saved at: ' + rawTestPath
                    : (result.status || 'Could not retrieve content') + (rawTestPath ? ' — ' + rawTestPath : '');
                tcBuildBanner.innerHTML = '&#x26A0;&#xFE0F; ' + escapeHtml(msg);
                tcBuildBanner.classList.remove('hidden');
                return;
            }
        }

        if (!content) {
            tcBuildBanner.innerHTML = '&#x26A0;&#xFE0F; No test content available.';
            tcBuildBanner.classList.remove('hidden');
            return;
        }

        const fileName = rawTestPath ? rawTestPath.split('/').pop() : 'testcases.spec.ts';
        const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    });

    // ── Test Cases: Copy ────────────────────────────────────────────────────
    tcCopyBtn.addEventListener('click', async () => {
        let content = rawTestContent;
        if (!content && rawTestPath) {
            const result = await fetchTestContent(rawTestPath);
            content = result.content;
        }
        if (!content) return;
        try {
            await navigator.clipboard.writeText(content);
            const original = tcCopyBtn.innerHTML;
            tcCopyBtn.textContent = '\u2713 Copied';
            setTimeout(() => { tcCopyBtn.innerHTML = original; }, 2000);
        } catch {
            const ta = document.createElement('textarea');
            ta.value = content;
            document.body.appendChild(ta);
            ta.select();
            document.execCommand('copy');
            document.body.removeChild(ta);
        }
    });

    // ── Test Cases: Back to Spec ────────────────────────────────────────────
    tcBackBtn.addEventListener('click', () => {
        testcasesSection.classList.add('hidden');
        resultSection.classList.remove('hidden');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });

    // ── View Test Cases (from spec panel) ───────────────────────────────────
    viewTestsBtn.addEventListener('click', () => {
        resultSection.classList.add('hidden');
        testcasesSection.classList.remove('hidden');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });

    // ── New Upload (from test cases panel) ─────────────────────────────────
    tcNewUploadBtn.addEventListener('click', () => {
        newUploadBtn.click();
    });

    // ── New Upload ──────────────────────────────────────────────────────────
    newUploadBtn.addEventListener('click', () => {
        clearFile();
        setUploading(false);
        setProgress(0, '');
        hideError();
        resultSection.classList.add('hidden');
        testcasesSection.classList.add('hidden');
        uploadSection.classList.remove('hidden');
        rawTestContent = '';
        rawTestPath    = '';
        viewTestsBtn.classList.add('hidden');
    });

})();

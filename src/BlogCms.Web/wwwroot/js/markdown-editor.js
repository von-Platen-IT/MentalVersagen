// Mentalversagen — Markdown-Editor (EasyMDE) für die Artikelverwaltung
//
// Progressive Enhancement: Ohne JavaScript (oder ohne EasyMDE) bleibt das
// normale Textfeld samt Datei-Upload vollständig nutzbar. Der Editor wird nur
// initialisiert, wenn das Feld #ContentMarkdown vorhanden ist.
//
// Funktionen:
// - Side-by-Side-Vorschau (formatted mode) über den serverseitigen Renderer
//   (/api/markdown/preview) — die Vorschau entspricht der späteren Ausgabe.
// - Bild-Button: AJAX-Upload (/api/media/upload), fügt ![alt](url) ein und
//   merkt sich die Asset-Id im Hidden-Feld #UploadedImageIds.
// - Video-Button: setzt das VideoUrl-Feld und fügt einen Markdown-Verweis ein.
// - Bildergalerie (nur Create): hochgeladene Bilder als Thumbnails; Klick fügt
//   das Bild an der Cursorposition ein.
(function () {
    'use strict';

    var textarea = document.getElementById('ContentMarkdown');
    if (!textarea || typeof window.EasyMDE === 'undefined') {
        return;
    }

    var form = textarea.closest('form');
    var hiddenIds = document.getElementById('UploadedImageIds');
    var hiddenJson = document.getElementById('UploadedImagesJson');
    var tokenInput = form ? form.querySelector('input[name="__RequestVerificationToken"]') : null;
    var token = tokenInput ? tokenInput.value : '';

    // --- Galerie-Elemente (nur auf der Create-Seite vorhanden) --------------
    var galleryRoot = document.querySelector('[data-editor-images]');
    var galleryGrid = document.querySelector('[data-editor-images-grid]');
    var galleryEmpty = document.querySelector('[data-editor-images-empty]');
    var galleryUpload = document.querySelector('[data-editor-images-upload]');

    var uploadedImages = [];
    if (hiddenJson && hiddenJson.value) {
        try {
            var parsed = JSON.parse(hiddenJson.value);
            if (Array.isArray(parsed)) {
                uploadedImages = parsed;
            }
        } catch (err) {
            uploadedImages = [];
        }
    }

    function altFromFile(file) {
        return (file && file.name ? file.name : '').replace(/\.[^.]+$/, '');
    }

    function addUploadedId(id) {
        if (!hiddenIds || !id) {
            return;
        }
        var ids = hiddenIds.value ? hiddenIds.value.split(',') : [];
        if (ids.indexOf(id) === -1) {
            ids.push(id);
        }
        hiddenIds.value = ids.join(',');
    }

    function persistGallery() {
        if (hiddenJson) {
            hiddenJson.value = JSON.stringify(uploadedImages);
        }
    }

    function renderGallery() {
        if (!galleryGrid) {
            return;
        }

        galleryGrid.textContent = '';
        uploadedImages.forEach(function (image) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'mv-create__images-item';
            button.title = 'An der Cursorposition einfügen';

            var img = document.createElement('img');
            img.src = image.url;
            img.alt = image.alt || '';
            img.loading = 'lazy';
            img.decoding = 'async';
            button.appendChild(img);

            button.addEventListener('click', function () {
                insertImageMarkdown(image);
            });

            galleryGrid.appendChild(button);
        });

        var hasImages = uploadedImages.length > 0;
        galleryGrid.hidden = !hasImages;
        if (galleryEmpty) {
            galleryEmpty.hidden = hasImages;
        }
    }

    function addImage(id, url, alt) {
        if (!id || !url) {
            return;
        }
        if (!uploadedImages.some(function (image) { return image.id === id; })) {
            uploadedImages.push({ id: id, url: url, alt: alt || '' });
        }
        addUploadedId(id);
        persistGallery();
        renderGallery();
    }

    function insertImageMarkdown(image) {
        var alt = image.alt || '';
        editor.codemirror.replaceSelection('![' + alt + '](' + image.url + ')');
        editor.codemirror.focus();
    }

    // --- Upload -------------------------------------------------------------
    function uploadFile(file) {
        var data = new FormData();
        data.append('file', file);

        return fetch('/api/media/upload', {
            method: 'POST',
            body: data,
            headers: token ? { 'RequestVerificationToken': token } : {}
        }).then(function (response) {
            return response.json().then(function (body) {
                if (!response.ok) {
                    throw new Error(body && body.error ? body.error : 'Upload fehlgeschlagen.');
                }
                return body;
            });
        });
    }

    function uploadImage(file, onSuccess, onError) {
        uploadFile(file)
            .then(function (result) {
                addImage(result.id, result.url, altFromFile(file));
                onSuccess(result.url);
            })
            .catch(function (error) {
                onError(error && error.message ? error.message : 'Upload fehlgeschlagen.');
            });
    }

    function pickAndUpload() {
        var input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';
        input.multiple = true;
        input.addEventListener('change', function () {
            Array.prototype.slice.call(input.files || []).forEach(function (file) {
                uploadFile(file)
                    .then(function (result) {
                        addImage(result.id, result.url, altFromFile(file));
                    })
                    .catch(function (error) {
                        window.alert(error && error.message ? error.message : 'Upload fehlgeschlagen.');
                    });
            });
        });
        input.click();
    }

    function insertVideo(editor) {
        var url = window.prompt('Video-URL (YouTube, Vimeo, X, TikTok):');
        if (!url) {
            return;
        }

        var field = document.getElementById('VideoUrl');
        if (field) {
            field.value = url;
            field.dispatchEvent(new Event('change', { bubbles: true }));
        }

        // Sichtbarer Verweis im Textlauf; das Video wird zusätzlich unter dem
        // Artikel eingebettet (bestehende oEmbed-Logik).
        editor.codemirror.replaceSelection('[▶ Video ansehen](' + url + ')');
    }

    // EasyMDE ruft previewRender synchron auf. Wir liefern daher den zuletzt
    // bekannten (server-gerenderten) Stand zurück und aktualisieren die Vorschau
    // entprellt per Fetch, sobald die Antwort da ist.
    var previewCache = '';
    var previewTimer = null;
    var previewTarget = null;

    function schedulePreview(plainText, preview) {
        previewTarget = preview;
        if (previewTimer) {
            window.clearTimeout(previewTimer);
        }
        previewTimer = window.setTimeout(function () {
            fetch('/api/markdown/preview', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                    'RequestVerificationToken': token
                },
                body: 'markdown=' + encodeURIComponent(plainText || '')
            })
                .then(function (response) {
                    return response.ok ? response.json() : { html: '' };
                })
                .then(function (result) {
                    previewCache = result.html || '';
                    if (previewTarget) {
                        previewTarget.innerHTML = previewCache;
                    }
                })
                .catch(function () {
                    // Fehler ignorieren: Die Vorschau bleibt beim letzten Stand.
                });
        }, 250);
    }

    function renderPreview(plainText, preview) {
        schedulePreview(plainText, preview);
        return previewCache;
    }

    var editor = new window.EasyMDE({
        element: textarea,
        autoDownloadFontAwesome: false,
        spellChecker: false,
        status: false,
        minHeight: '320px',
        placeholder: 'Akte verfassen …',
        toolbar: [
            'bold', 'italic', 'heading', '|',
            'quote', 'unordered-list', 'ordered-list', '|',
            'link', 'image',
            {
                name: 'video',
                action: insertVideo,
                className: 'fa fa-video-camera',
                title: 'Video einfügen'
            },
            '|',
            'preview', 'side-by-side', 'fullscreen', '|',
            'guide'
        ],
        imageUploadFunction: uploadImage,
        previewRender: renderPreview
    });

    // Synchronisiere den Inhalt bei Änderungen und verbindlich beim Absenden des Formulars.
    editor.codemirror.on('change', function () {
        textarea.value = editor.value();
    });

    if (form) {
        form.addEventListener('submit', function () {
            textarea.value = editor.value();
        });
    }

    // Galerie initialisieren und Upload-Button verdrahten.
    if (galleryRoot) {
        renderGallery();
        if (galleryUpload) {
            galleryUpload.addEventListener('click', pickAndUpload);
        }
    }
})();

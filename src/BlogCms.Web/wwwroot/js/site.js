// Mentalversagen — Progressive Enhancement
//
// 1. Scroll-Reveal für Archiv-Elemente (.mv-case, .mv-doc, .mv-teaser,
//    .mv-filter, .mv-comment). Ohne JavaScript bleibt der Inhalt vollständig
//    sichtbar: Die Verbergungs-Klassen (.mv-reveal/.mv-lift) werden erst hier
//    gesetzt. Bei prefers-reduced-motion oder fehlendem IntersectionObserver
//    wird das Enhancement komplett übersprungen.
// 2. Lightbox für Bild-Thumbnails (Artikel-Galerie und Kommentarbilder).
// 3. „Markdown kopieren"-Buttons der Admin-Bilderverwaltung.
// 4. Hashtag-Autocomplete und REST-Grid der Startseite.
// 5. Sicherheitsabfrage der Registrierung (Bedienkomfort, Prüfung im Server).
// 6. Vorprüfung der Dateigröße bei Bild-Uploads (verbindlich prüft der Server).

(function () {
    'use strict';

    if (!('IntersectionObserver' in window)) {
        return;
    }

    var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (reduceMotion.matches) {
        return;
    }

    var targets = document.querySelectorAll(
        '.mv-case, .mv-doc, .mv-teaser, .mv-filter, .mv-comment'
    );
    if (targets.length === 0) {
        return;
    }

    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (!entry.isIntersecting) {
                return;
            }
            entry.target.classList.add('is-visible');
            observer.unobserve(entry.target);
        });
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.05 });

    targets.forEach(function (el) {
        el.classList.add('mv-reveal');
        if (!el.classList.contains('mv-case')) {
            el.classList.add('mv-lift');
        }
        observer.observe(el);
    });
})();

// Mentalversagen — Sicherheitsabfrage der Registrierung
//
// WICHTIG: Die eigentliche Prüfung findet immer auf dem Server statt (signierter
// Token + Mindestzeit). Dieses Modul verbessert nur die Bedienung: Der Absenden-
// Button wird erst nach einer Eingabe freigegeben, es gibt Live-Feedback und
// einen Knopf für eine neue Aufgabe. Ohne JavaScript bleibt das Formular
// vollständig nutzbar.
(function () {
    'use strict';

    var root = document.querySelector('[data-captcha]');
    if (!root) {
        return;
    }

    var input = root.querySelector('input[name="CaptchaAnswer"]');
    var tokenField = root.querySelector('input[name="CaptchaToken"]');
    var question = root.querySelector('[data-captcha-question]');
    var hint = root.querySelector('[data-captcha-hint]');
    var refresh = root.querySelector('[data-captcha-refresh]');
    var submit = document.querySelector('[data-captcha-submit]');
    var endpoint = root.getAttribute('data-captcha-endpoint');

    var defaultHint = hint ? hint.textContent : '';

    function updateSubmitState() {
        if (!submit || !input) {
            return;
        }
        submit.disabled = input.value.trim().length === 0;
    }

    if (input) {
        input.addEventListener('input', function () {
            updateSubmitState();
            if (hint) {
                hint.textContent = input.value.trim().length > 0
                    ? 'Antwort eingetragen — geprüft wird sie beim Absenden.'
                    : defaultHint;
            }
        });
        updateSubmitState();
    }

    if (refresh && endpoint) {
        refresh.addEventListener('click', function () {
            refresh.disabled = true;

            fetch(endpoint, { headers: { 'Accept': 'application/json' } })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (data) {
                    if (!data) {
                        return;
                    }
                    if (question) {
                        question.textContent = data.question;
                    }
                    if (tokenField) {
                        tokenField.value = data.token;
                    }
                    if (input) {
                        input.value = '';
                        input.focus();
                    }
                    if (hint) {
                        hint.textContent = defaultHint;
                    }
                    updateSubmitState();
                })
                .catch(function () {
                    // Fehler ignorieren: Der Server prüft die Aufgabe ohnehin.
                })
                .then(function () { refresh.disabled = false; });
        });
    }
})();

// Mentalversagen — Hashtag-Autocomplete und REST-Grid (Startseite)
//
// Nach dem Laden holt die Seite die Hashtag-Liste per REST (/api/hashtags).
// Beim Tippen erscheint ein Dropdown mit passenden Hashtags (Maus, Pfeiltasten,
// Enter, Escape). Eine Auswahl oder eine geänderte Sortierung aktualisiert das
// Grid per REST (/api/articles) ohne Neuladen. Ohne JavaScript bleibt das
// serverseitig gerenderte Grid sichtbar und die Suchbox ist ein normales Feld.
(function () {
    'use strict';

    var tools = document.querySelector('[data-article-tools]');
    if (!tools) {
        return;
    }

    var grid = document.querySelector('[data-article-grid]');
    var emptyMessage = document.querySelector('[data-grid-empty]');
    var heading = document.querySelector('[data-grid-heading]');
    var status = document.querySelector('[data-filter-status]');
    var searchRoot = tools.querySelector('[data-autocomplete]');
    var input = searchRoot ? searchRoot.querySelector('.mv-search__input') : null;
    var list = searchRoot ? searchRoot.querySelector('.mv-search__list') : null;
    var sortSelect = tools.querySelector('.mv-sort__select');

    var apiUrl = tools.getAttribute('data-api') || '/api/articles';
    var hashtagApiUrl = tools.getAttribute('data-hashtag-api') || '/api/hashtags';
    var basePageSize = parseInt(tools.getAttribute('data-page-size'), 10) || 6;
    var filteredPageSize = parseInt(tools.getAttribute('data-filtered-page-size'), 10) || 12;

    var hashtags = [];
    var activeHashtag = null;
    var activeIndex = -1;
    var requestCounter = 0;

    function toArray(nodeList) {
        return Array.prototype.slice.call(nodeList);
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined && text !== null) {
            node.textContent = text;
        }
        return node;
    }

    function articleUrl(slug) {
        return '/Articles/Details?slug=' + encodeURIComponent(slug || '');
    }

    // --- Hashtag-Liste per REST laden -------------------------------------
    fetch(hashtagApiUrl, { headers: { 'Accept': 'application/json' } })
        .then(function (response) { return response.ok ? response.json() : []; })
        .then(function (data) { hashtags = Array.isArray(data) ? data : []; })
        .catch(function () { hashtags = []; });

    // --- Grid per REST aktualisieren --------------------------------------
    function loadArticles() {
        var params = new URLSearchParams();
        if (activeHashtag) {
            params.set('hashtag', activeHashtag.slug);
        }
        if (sortSelect && sortSelect.value) {
            params.set('sort', sortSelect.value);
        }
        params.set('pageSize', String(activeHashtag ? filteredPageSize : basePageSize));

        var requestId = ++requestCounter;
        if (grid) {
            grid.setAttribute('aria-busy', 'true');
        }

        fetch(apiUrl + '?' + params.toString(), { headers: { 'Accept': 'application/json' } })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                if (requestId !== requestCounter || !data) {
                    return;
                }
                renderGrid(data.items || []);
                updateStatus(data.total || 0);
                if (grid) {
                    grid.removeAttribute('aria-busy');
                }
            })
            .catch(function () {
                // Fehler: das bisherige Grid bleibt unverändert stehen.
                if (grid) {
                    grid.removeAttribute('aria-busy');
                }
            });
    }

    function renderGrid(items) {
        if (!grid) {
            return;
        }

        grid.textContent = '';
        items.forEach(function (article) {
            grid.appendChild(renderCard(article));
        });

        if (emptyMessage) {
            emptyMessage.hidden = items.length > 0;
        }
        if (heading) {
            heading.textContent = activeHashtag ? 'Akten zu #' + activeHashtag.name : 'Neueste Akten';
        }
    }

    function renderCard(article) {
        var card = el('article', 'mv-case');

        var head = el('div', 'mv-case__head');
        head.appendChild(el('span', null, 'ARCHIV ' + String(article.category || '').toUpperCase()));
        head.appendChild(el('span', 'text-end', '#' + (article.archiveCode || '')));
        card.appendChild(head);

        if (article.imageUrl) {
            var imageLink = el('a', 'mv-case__img');
            imageLink.href = articleUrl(article.slug);
            var image = el('img');
            image.src = article.imageUrl;
            image.alt = article.title || '';
            image.loading = 'lazy';
            image.decoding = 'async';
            imageLink.appendChild(image);
            card.appendChild(imageLink);
        }

        var body = el('div', 'mv-case__body');
        var title = el('h3', 'mv-case__title');
        var titleLink = el('a', null, article.title || '');
        titleLink.href = articleUrl(article.slug);
        title.appendChild(titleLink);
        body.appendChild(title);

        if (article.excerpt) {
            body.appendChild(el('p', 'mv-case__excerpt', article.excerpt));
        }

        var meta = el('div', 'mv-case__meta');
        meta.appendChild(el('span', 'mv-badge mv-badge--open', '? OFFEN'));
        meta.appendChild(el('span', null, article.accessLabel || ''));
        meta.appendChild(el('span', null, article.publishedAt || ''));
        body.appendChild(meta);
        card.appendChild(body);

        var footer = el('div', 'mv-case__footer');
        footer.appendChild(el('span', null, article.author || 'Archiv'));
        var next = el('a', 'mv-case__next', 'WEITER \u2192');
        next.href = articleUrl(article.slug);
        footer.appendChild(next);
        card.appendChild(footer);

        return card;
    }

    function updateStatus(total) {
        if (!status) {
            return;
        }

        status.textContent = '';
        if (!activeHashtag) {
            status.hidden = true;
            return;
        }

        status.hidden = false;
        status.appendChild(document.createTextNode(
            'Gefiltert nach #' + activeHashtag.name + ' \u00b7 ' + total + ' Treffer'));

        var reset = el('a', 'mv-filter-status__reset', 'Zur\u00fccksetzen');
        reset.href = '#';
        reset.addEventListener('click', function (event) {
            event.preventDefault();
            clearFilter();
        });
        status.appendChild(reset);
    }

    // --- Autocomplete ------------------------------------------------------
    function openList() {
        if (!list || !input) {
            return;
        }
        list.hidden = false;
        input.setAttribute('aria-expanded', 'true');
    }

    function closeList() {
        if (!list || !input) {
            return;
        }
        list.hidden = true;
        input.setAttribute('aria-expanded', 'false');
        activeIndex = -1;
    }

    function renderOptions(term) {
        if (!list) {
            return;
        }

        var query = String(term || '').replace(/^#/, '').trim().toLowerCase();
        var matches = hashtags.filter(function (hashtag) {
            return !query || hashtag.name.toLowerCase().indexOf(query) !== -1;
        }).slice(0, 8);

        list.textContent = '';
        activeIndex = -1;

        if (matches.length === 0) {
            list.appendChild(el('li', 'mv-search__empty', 'Keine passenden Hashtags'));
            openList();
            return;
        }

        matches.forEach(function (hashtag) {
            var option = el('li', 'mv-search__option');
            option.setAttribute('role', 'option');
            option.setAttribute('aria-selected', 'false');
            option.__hashtag = hashtag;
            option.appendChild(el('span', null, '#' + hashtag.name));
            option.appendChild(el('span', 'mv-search__option-count', String(hashtag.count)));
            option.addEventListener('mousedown', function (event) {
                // Fokus im Eingabefeld halten, damit blur das Dropdown nicht schließt.
                event.preventDefault();
                selectHashtag(hashtag);
            });
            list.appendChild(option);
        });

        openList();
    }

    function highlightOption() {
        if (!list) {
            return;
        }

        toArray(list.querySelectorAll('.mv-search__option')).forEach(function (option, index) {
            var selected = index === activeIndex;
            option.setAttribute('aria-selected', selected ? 'true' : 'false');
            if (selected && option.scrollIntoView) {
                option.scrollIntoView({ block: 'nearest' });
            }
        });
    }

    function selectHashtag(hashtag) {
        activeHashtag = { name: hashtag.name, slug: hashtag.slug };
        if (input) {
            input.value = '#' + hashtag.name;
        }
        closeList();
        loadArticles();
    }

    function clearFilter() {
        activeHashtag = null;
        if (input) {
            input.value = '';
        }
        closeList();
        loadArticles();
    }

    if (input && list) {
        input.addEventListener('input', function () { renderOptions(input.value); });
        input.addEventListener('focus', function () { renderOptions(input.value); });
        input.addEventListener('blur', function () { window.setTimeout(closeList, 120); });
        input.addEventListener('keydown', function (event) {
            var options = toArray(list.querySelectorAll('.mv-search__option'));

            if (event.key === 'ArrowDown') {
                event.preventDefault();
                if (list.hidden) {
                    renderOptions(input.value);
                    return;
                }
                activeIndex = Math.min(activeIndex + 1, options.length - 1);
                highlightOption();
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                activeIndex = Math.max(activeIndex - 1, 0);
                highlightOption();
            } else if (event.key === 'Enter') {
                if (activeIndex >= 0 && options[activeIndex] && options[activeIndex].__hashtag) {
                    event.preventDefault();
                    selectHashtag(options[activeIndex].__hashtag);
                }
            } else if (event.key === 'Escape') {
                closeList();
            }
        });
    }

    if (sortSelect) {
        sortSelect.addEventListener('change', function () { loadArticles(); });
    }
})();

// Mentalversagen — Lightbox für Bild-Thumbnails (Progressive Enhancement)
//
// Gilt für alle Links mit data-lightbox: Artikel-Galerie (.mv-gallery__item)
// und Kommentar-Thumbnails (.mv-thumb). Ohne JavaScript öffnet der Link die
// Bilddatei direkt (href). Mit JavaScript wird ein barrierefreies Overlay
// geöffnet:
// - ESC und Klick auf den Hintergrund schließen
// - Der Fokus wird im Dialog gehalten und beim Schließen zurückgegeben
// - role="dialog" + aria-modal für Screenreader
(function () {
    'use strict';

    var links = Array.prototype.slice.call(document.querySelectorAll('a[data-lightbox]'));
    if (links.length === 0) {
        return;
    }

    function closeLightbox(overlay, previouslyFocused, onKey) {
        overlay.remove();
        document.removeEventListener('keydown', onKey);
        if (previouslyFocused && typeof previouslyFocused.focus === 'function') {
            previouslyFocused.focus();
        }
    }

    function openLightbox(link) {
        var thumb = link.querySelector('img');
        var alt = thumb ? (thumb.alt || '') : '';

        var overlay = document.createElement('div');
        overlay.className = 'mv-lightbox';
        overlay.setAttribute('role', 'dialog');
        overlay.setAttribute('aria-modal', 'true');
        overlay.setAttribute('aria-label', 'Bildansicht');

        var full = document.createElement('img');
        full.className = 'mv-lightbox__img';
        full.src = link.getAttribute('href');
        full.alt = alt;

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn btn-outline-secondary mv-lightbox__close';
        close.textContent = 'Schließen ✕';

        var previouslyFocused = document.activeElement;

        function onKey(e) {
            if (e.key === 'Escape') {
                e.preventDefault();
                closeLightbox(overlay, previouslyFocused, onKey);
            } else if (e.key === 'Tab') {
                // Einfacher Fokus-Fang: nur der Schließen-Button ist bedienbar.
                e.preventDefault();
                close.focus();
            }
        }

        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) {
                closeLightbox(overlay, previouslyFocused, onKey);
            }
        });
        close.addEventListener('click', function () {
            closeLightbox(overlay, previouslyFocused, onKey);
        });

        overlay.appendChild(full);
        if (alt) {
            var caption = document.createElement('p');
            caption.className = 'mv-lightbox__caption';
            caption.textContent = alt;
            overlay.appendChild(caption);
        }
        overlay.appendChild(close);
        document.body.appendChild(overlay);
        close.focus();
        document.addEventListener('keydown', onKey);
    }

    links.forEach(function (link) {
        link.addEventListener('click', function (e) {
            e.preventDefault();
            openLightbox(link);
        });
    });
})();

// Mentalversagen — „Markdown kopieren"-Buttons der Admin-Bilderverwaltung
//
// Kopiert das Markdown-Snippet (input value) in die Zwischenablage.
// Fallback ohne Clipboard-API: Das Feld wird selektiert, sodass der
// Text manuell kopiert werden kann.
(function () {
    'use strict';

    var buttons = Array.prototype.slice.call(document.querySelectorAll('[data-copy]'));
    if (buttons.length === 0) {
        return;
    }

    buttons.forEach(function (btn) {
        btn.addEventListener('click', function () {
            var input = btn.parentElement && btn.parentElement.querySelector('input');
            if (!input) {
                return;
            }

            function done() {
                var original = btn.textContent;
                btn.textContent = 'Kopiert ✓';
                window.setTimeout(function () { btn.textContent = original; }, 1600);
            }

            input.select();
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(input.value).then(done, done);
            } else {
                try {
                    document.execCommand('copy');
                } catch (err) { /* Auswahl bleibt sichtbar — manuelles Kopieren möglich */ }
                done();
            }
        });
    });
})();

// Mentalversagen — Vorprüfung der Dateigröße bei Bild-Uploads
//
// Gibt sofort Rückmeldung, bevor das Formular abgeschickt wird. Die
// verbindliche Prüfung bleibt serverseitig (MediaService) — dieses Modul
// erspart dem Nutzer nur den Umweg über den Server.
(function () {
    'use strict';

    var inputs = Array.prototype.slice.call(
        document.querySelectorAll('input[type="file"][data-max-bytes]'));
    if (inputs.length === 0) {
        return;
    }

    inputs.forEach(function (input) {
        var maxBytes = parseInt(input.getAttribute('data-max-bytes'), 10) || 0;
        var maxLabel = input.getAttribute('data-max-label') || '';
        var hint = input.parentElement
            ? input.parentElement.querySelector('[data-file-hint]')
            : null;
        var defaultHint = hint ? hint.textContent : '';

        function reset() {
            if (hint) {
                hint.textContent = defaultHint;
                hint.classList.remove('text-danger');
            }
            input.setCustomValidity('');
        }

        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            if (!file) {
                reset();
                return;
            }

            if (maxBytes > 0 && file.size > maxBytes) {
                var sizeMb = (file.size / (1024 * 1024)).toFixed(1);
                var message = 'Das Bild ist zu groß (' + sizeMb + ' MB). Erlaubt sind maximal '
                    + maxLabel + '.';

                if (hint) {
                    hint.textContent = message;
                    hint.classList.add('text-danger');
                }
                input.setCustomValidity(message);
                input.reportValidity();
                return;
            }

            reset();
        });
    });
})();

window.mailEditor = (() => {
    const editorStateMap = new WeakMap();

    const selectionMarkerAttributeName = "data-mail-editor-marker";
    const zeroWidthSpaceCharacter = "\u200B";
    const emptyEditorHtml = "<p><br></p>";
    const maxHistorySnapshots = 100;
    const historyDebounceDelayInMilliseconds = 250;
    const cleanupDebounceDelayInMilliseconds = 150;
    const previewDebounceDelayInMilliseconds = 75;

    const allowedTagNames = new Set([
        "p",
        "br",
        "strong",
        "b",
        "em",
        "i",
        "u",
        "a",
        "ul",
        "ol",
        "li",
        "h1",
        "h2",
        "h3",
        "blockquote",
        "hr",
        "img",
        "span"
    ]);

    const blockTagNames = new Set([
        "p",
        "div",
        "h1",
        "h2",
        "h3",
        "blockquote",
        "ul",
        "ol",
        "li"
    ]);

    const editableBlockTagNames = new Set(["p", "div", "h1", "h2", "h3", "blockquote"]);
    const removableContainerTagNames = ["script", "style", "iframe", "object", "embed", "meta", "link"];

    /**
     * Returns the per-editor state object and creates it when needed.
     *
     * @param {HTMLElement} editorElement
     * @returns {{
     *   history: Array<{ html: string, selection: { startMarkerId: string, endMarkerId: string } | null }>,
     *   historyIndex: number,
     *   isApplyingHistory: boolean,
     *   inputDebounceTimeout: number | null,
     *   cleanupDebounceTimeout: number | null,
     *   savedSelectionRange: Range | null
     * }}
     */
    function getEditorState(editorElement) {
        let state = editorStateMap.get(editorElement);

        if (!state) {
            state = {
                history: [],
                historyIndex: -1,
                isApplyingHistory: false,
                inputDebounceTimeout: null,
                cleanupDebounceTimeout: null,
                previewDebounceTimeout: null,
                dotNetReference: null,
                savedSelectionRange: null
            };

            editorStateMap.set(editorElement, state);
        }

        return state;
    }

    /**
     * Syncs the current editor HTML and selection back to Blazor.
     *
     * @param {HTMLElement} editorElement
     */
    function syncEditor(editorElement) {
        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);
    }

    /**
     * @returns {Selection | null}
     */
    function getSelectionInstance() {
        return window.getSelection();
    }

    /**
     * Returns whether a range still points to nodes inside the editor.
     *
     * @param {HTMLElement} editorElement
     * @param {Range | null | undefined} range
     * @returns {boolean}
     */
    function isRangeInsideEditor(editorElement, range) {
        if (!editorElement || !range) {
            return false;
        }

        return (
            editorElement.contains(range.startContainer) &&
            editorElement.contains(range.endContainer)
        );
    }

    /**
     * Returns the current live browser selection only when it belongs to the editor.
     *
     * @param {HTMLElement} editorElement
     * @returns {Range | null}
     */
    function getLiveSelectionRange(editorElement) {
        if (!editorElement) {
            return null;
        }

        const selection = getSelectionInstance();

        if (!selection || selection.rangeCount === 0) {
            return null;
        }

        const range = selection.getRangeAt(0);

        return isRangeInsideEditor(editorElement, range)
            ? range
            : null;
    }

    /**
     * Clears the last saved selection for an editor.
     *
     * @param {HTMLElement} editorElement
     */
    function clearSavedSelectionRange(editorElement) {
        const state = getEditorState(editorElement);
        state.savedSelectionRange = null;
    }

    /**
     * Stores a clone of the provided range so toolbar actions can restore it later.
     *
     * @param {HTMLElement} editorElement
     * @param {Range | null | undefined} range
     */
    function saveSelectionRange(editorElement, range) {
        const state = getEditorState(editorElement);

        if (!isRangeInsideEditor(editorElement, range)) {
            state.savedSelectionRange = null;
            return;
        }

        state.savedSelectionRange = range.cloneRange();
    }

    /**
     * Stores the current live selection for later restoration.
     *
     * @param {HTMLElement} editorElement
     */
    function saveCurrentSelectionRange(editorElement) {
        const liveRange = getLiveSelectionRange(editorElement);

        if (!liveRange) {
            return;
        }

        saveSelectionRange(editorElement, liveRange);
    }

    /**
     * Restores the last saved selection back into the browser selection.
     *
     * @param {HTMLElement} editorElement
     * @returns {Range | null}
     */
    function restoreSavedSelectionRange(editorElement) {
        const state = getEditorState(editorElement);
        const savedRange = state.savedSelectionRange;

        if (!isRangeInsideEditor(editorElement, savedRange)) {
            state.savedSelectionRange = null;
            return null;
        }

        const selection = getSelectionInstance();
        if (!selection) {
            return null;
        }

        const restoredRange = savedRange.cloneRange();
        selection.removeAllRanges();
        selection.addRange(restoredRange);
        state.savedSelectionRange = restoredRange.cloneRange();

        return restoredRange;
    }

    /**
     * Returns the current selection range only when it belongs to the editor.
     *
     * @param {HTMLElement} editorElement
     * @returns {Range | null}
     */
    function getSelectionRange(editorElement) {
        if (!editorElement) {
            return null;
        }

        const liveRange = getLiveSelectionRange(editorElement);
        if (liveRange) {
            saveSelectionRange(editorElement, liveRange);
            return liveRange;
        }

        const state = getEditorState(editorElement);

        if (!isRangeInsideEditor(editorElement, state.savedSelectionRange)) {
            state.savedSelectionRange = null;
            return null;
        }

        return state.savedSelectionRange.cloneRange();
    }

    /**
     * Moves focus back to the editor before DOM mutations that rely on selection.
     *
     * @param {HTMLElement} editorElement
     */
    function focusEditor(editorElement) {
        if (editorElement) {
            editorElement.focus();

            if (getLiveSelectionRange(editorElement)) {
                saveCurrentSelectionRange(editorElement);
            } else {
                restoreSavedSelectionRange(editorElement);
            }
        }
    }

    /**
     * Selects the full contents of a node.
     *
     * @param {Node} node
     */
    function selectNodeContents(node) {
        const selection = getSelectionInstance();
        const range = document.createRange();

        range.selectNodeContents(node);
        selection.removeAllRanges();
        selection.addRange(range);
    }

    /**
     * Places the caret at the start of the given element.
     *
     * @param {HTMLElement} element
     */
    function placeCaretInsideElement(element) {
        if (!element) {
            return;
        }

        const selection = getSelectionInstance();
        const range = document.createRange();

        if (element.firstChild) {
            range.setStart(element.firstChild, 0);
        } else {
            const textNode = document.createTextNode("");
            element.appendChild(textNode);
            range.setStart(textNode, 0);
        }

        range.collapse(true);
        selection.removeAllRanges();
        selection.addRange(range);
    }

    /**
     * Finds the nearest ancestor matching a tag name within the editor boundary.
     *
     * @param {Node} node
     * @param {string} tagName
     * @param {HTMLElement} editorElement
     * @returns {HTMLElement | null}
     */
    function getClosestAncestorTag(node, tagName, editorElement) {
        let currentNode = node;

        if (currentNode?.nodeType === Node.TEXT_NODE) {
            currentNode = currentNode.parentNode;
        }

        const normalizedTagName = tagName.toLowerCase();

        while (currentNode && currentNode !== editorElement) {
            if (
                currentNode.nodeType === Node.ELEMENT_NODE &&
                currentNode.tagName.toLowerCase() === normalizedTagName
            ) {
                return currentNode;
            }

            currentNode = currentNode.parentNode;
        }

        return null;
    }

    /**
     * Finds the nearest ancestor matching any of the given tag names within the editor boundary.
     *
     * @param {Node} node
     * @param {string[]} tagNames
     * @param {HTMLElement} editorElement
     * @returns {HTMLElement | null}
     */
    function getClosestAncestorByTags(node, tagNames, editorElement) {
        let currentNode = node;

        if (currentNode?.nodeType === Node.TEXT_NODE) {
            currentNode = currentNode.parentNode;
        }

        const normalizedTagNames = new Set(tagNames.map(tagName => tagName.toLowerCase()));

        while (currentNode && currentNode !== editorElement) {
            if (
                currentNode.nodeType === Node.ELEMENT_NODE &&
                normalizedTagNames.has(currentNode.tagName.toLowerCase())
            ) {
                return currentNode;
            }

            currentNode = currentNode.parentNode;
        }

        return null;
    }

    /**
     * Removes an element but keeps its child nodes in place.
     *
     * @param {HTMLElement} element
     */
    function unwrapElement(element) {
        if (!element?.parentNode) {
            return;
        }

        while (element.firstChild) {
            element.parentNode.insertBefore(element.firstChild, element);
        }

        element.remove();
    }

    /**
     * Escapes a string for safe HTML insertion.
     *
     * @param {string | null | undefined} value
     * @returns {string}
     */
    function escapeHtml(value) {
        const temporaryElement = document.createElement("div");
        temporaryElement.textContent = value ?? "";
        return temporaryElement.innerHTML;
    }

    /**
     * Converts plain text into editor-friendly HTML paragraphs.
     *
     * @param {string} plainText
     * @returns {string}
     */
    function convertPlainTextToHtml(plainText) {
        if (!plainText) {
            return "";
        }

        return plainText
            .replace(/\r\n/g, "\n")
            .replace(/\r/g, "\n")
            .split("\n\n")
            .map(paragraphText => `<p>${escapeHtml(paragraphText).replace(/\n/g, "<br>")}</p>`)
            .join("");
    }

    /**
     * Rejects dangerous protocols while keeping common safe URLs intact.
     *
     * @param {string} url
     * @returns {string}
     */
    function sanitizeUrl(url) {
        if (!url) {
            return "";
        }

        const trimmedUrl = url.trim();

        if (!trimmedUrl) {
            return "";
        }

        const lowerCasedUrl = trimmedUrl.toLowerCase();

        if (
            lowerCasedUrl.startsWith("javascript:") ||
            lowerCasedUrl.startsWith("data:text/html") ||
            lowerCasedUrl.startsWith("vbscript:")
        ) {
            return "";
        }

        return trimmedUrl;
    }

    /**
     * Sanitizes a DOM node recursively into the subset of tags and attributes the editor supports.
     *
     * @param {Node} node
     * @returns {Node}
     */
    function sanitizeNode(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            return document.createTextNode(node.textContent || "");
        }

        if (node.nodeType !== Node.ELEMENT_NODE) {
            return document.createDocumentFragment();
        }

        const sourceElement = /** @type {HTMLElement} */ (node);
        const tagName = sourceElement.tagName.toLowerCase();

        if (removableContainerTagNames.includes(tagName)) {
            return document.createDocumentFragment();
        }

        if (!allowedTagNames.has(tagName)) {
            const fragment = document.createDocumentFragment();

            Array.from(sourceElement.childNodes).forEach(childNode => {
                fragment.appendChild(sanitizeNode(childNode));
            });

            return fragment;
        }

        const sanitizedElement = document.createElement(tagName === "div" ? "p" : tagName);
        copyAllowedInlineStyles(sourceElement, sanitizedElement);

        if (tagName === "span" && sourceElement.classList.contains("mail-merge-token")) {
            sanitizedElement.setAttribute("class", "mail-merge-token");
            sanitizedElement.setAttribute("contenteditable", "false");

            const tokenValue = sourceElement.getAttribute("data-token") || "";
            if (tokenValue) {
                sanitizedElement.setAttribute("data-token", tokenValue);
            }
        } else if (tagName === "span" && sourceElement.classList.contains("mail-text-color")) {
            const colorValue = sourceElement.getAttribute("data-text-color") || "";
            if (!/^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(colorValue)) {
                return document.createDocumentFragment();
            }

            sanitizedElement.setAttribute("class", "mail-text-color");
            sanitizedElement.setAttribute("data-text-color", colorValue);
            sanitizedElement.style.color = colorValue;
        } else if (tagName === "span" && sourceElement.hasAttribute(selectionMarkerAttributeName)) {
            sanitizedElement.setAttribute(
                selectionMarkerAttributeName,
                sourceElement.getAttribute(selectionMarkerAttributeName) || ""
            );
        } else if (tagName === "span") {
            const fragment = document.createDocumentFragment();

            Array.from(sourceElement.childNodes).forEach(childNode => {
                fragment.appendChild(sanitizeNode(childNode));
            });

            return fragment;
        }

        if (tagName === "a") {
            const hrefValue = sanitizeUrl(sourceElement.getAttribute("href"));
            if (hrefValue) {
                sanitizedElement.setAttribute("href", hrefValue);
                sanitizedElement.setAttribute("target", "_blank");
                sanitizedElement.setAttribute("rel", "noopener noreferrer");
            }
        }

        if (tagName === "img") {
            const sourceValue = sanitizeUrl(sourceElement.getAttribute("src"));
            if (!sourceValue) {
                return document.createDocumentFragment();
            }

            sanitizedElement.setAttribute("src", sourceValue);
            sanitizedElement.setAttribute("alt", sourceElement.getAttribute("alt") || "");

            if (sourceElement.classList.contains("mail-editor-image")) {
                sanitizedElement.setAttribute("class", "mail-editor-image");
            }
        }

        Array.from(sourceElement.childNodes).forEach(childNode => {
            sanitizedElement.appendChild(sanitizeNode(childNode));
        });

        return sanitizedElement;
    }

    /**
     * Sanitizes arbitrary HTML into editor-safe HTML.
     *
     * @param {string} html
     * @returns {string}
     */
    function sanitizeHtml(html) {
        const parser = new DOMParser();
        const parsedDocument = parser.parseFromString(html || "", "text/html");
        const temporaryContainer = document.createElement("div");

        Array.from(parsedDocument.body.childNodes).forEach(childNode => {
            temporaryContainer.appendChild(sanitizeNode(childNode));
        });

        return temporaryContainer.innerHTML;
    }

    /**
     * Notifies Blazor that the editor HTML changed.
     */
    function notifyEditorChanged(editorElement) {
        const state = getEditorState(editorElement);

        if (!state.dotNetReference) {
                     return;
        }

        state.dotNetReference.invokeMethodAsync(
            "HandleEditorChangedAsync",
            editorElement.innerHTML
        );
    }

    /**
     * Notifies Blazor that the current selection/caret changed.
     */
    function notifySelectionChanged(editorElement) {
        const state = getEditorState(editorElement);

        if (!state.dotNetReference) {
            return;
        }

        state.dotNetReference.invokeMethodAsync("HandleSelectionChangedAsync");
    }

    /**
     * Removes an element while preserving its children.
     *
     * @param {HTMLElement} element
     */
    function unwrapElementPreservingChildren(element) {
        if (!element?.parentNode) {
            return;
        }

        while (element.firstChild) {
            element.parentNode.insertBefore(element.firstChild, element);
        }

        element.remove();
    }

    /**
     * Converts stray top-level text nodes into paragraphs to keep the document structure predictable.
     *
     * @param {HTMLElement} containerElement
     */
    function normalizeTopLevelTextNodes(containerElement) {
        Array.from(containerElement.childNodes).forEach(childNode => {
            if (childNode.nodeType !== Node.TEXT_NODE) {
                return;
            }

            const textValue = childNode.textContent || "";

            if (!textValue.trim()) {
                childNode.remove();
                return;
            }

            const paragraphElement = document.createElement("p");
            paragraphElement.textContent = textValue;
            childNode.replaceWith(paragraphElement);
        });
    }

    /**
     * Removes empty formatting tags that add no visual or semantic value.
     *
     * @param {HTMLElement} containerElement
     */
    function removeEmptyInlineElements(containerElement) {
        const inlineSelector = "strong, b, em, i, u, a, span";

        Array.from(containerElement.querySelectorAll(inlineSelector)).forEach(element => {
            if (element.classList.contains("mail-merge-token")) {
                return;
            }

            if (element.classList.contains("mail-text-color")) {
                return;
            }

            if (element.tagName.toLowerCase() === "a" && element.getAttribute("href")) {
                return;
            }

            if (element.classList.contains("mail-text-color")) {
                return;
            }
            
            const cleanedText = (element.textContent || "")
                .replace(/\u200B/g, "")
                .replace(/\u00A0/g, " ")
                .trim();

            const hasChildren = element.children.length > 0;

            if (!cleanedText && !hasChildren) {
                element.remove();
            }
        });
    }

    /**
     * Merges sibling inline tags of the same type to reduce noisy HTML output.
     *
     * @param {HTMLElement} containerElement
     * @param {string} tagName
     */
    function mergeAdjacentInlineElements(containerElement, tagName) {
        Array.from(containerElement.querySelectorAll(tagName)).forEach(element => {
            let nextSibling = element.nextSibling;

            while (
                nextSibling &&
                nextSibling.nodeType === Node.ELEMENT_NODE &&
                nextSibling.tagName.toLowerCase() === tagName.toLowerCase()
                ) {
                const siblingToMerge = nextSibling;

                while (siblingToMerge.firstChild) {
                    element.appendChild(siblingToMerge.firstChild);
                }

                nextSibling = siblingToMerge.nextSibling;
                siblingToMerge.remove();
            }
        });
    }

    function copyAllowedInlineStyles(sourceElement, targetElement) {
        if (!sourceElement?.style || !targetElement) {
            return;
        }

        const allowedStyleProperties = [
            "color",
            "backgroundColor",
            "textAlign",
            "fontSize",
            "fontWeight",
            "fontStyle",
            "textDecoration"
        ];

        for (const styleProperty of allowedStyleProperties) {
            const styleValue = sourceElement.style[styleProperty];

            if (styleValue) {
                targetElement.style[styleProperty] = styleValue;
            }
        }
    }

    /**
     * Removes empty block elements unless they still contain meaningful visual content.
     *
     * @param {HTMLElement} containerElement
     */
    function removeEmptyBlocks(containerElement) {
        const selector = "p, h1, h2, h3, blockquote, li";

        Array.from(containerElement.querySelectorAll(selector)).forEach(element => {
            const hasImage = !!element.querySelector("img");
            const hasToken = !!element.querySelector(".mail-merge-token");
            const textValue = (element.textContent || "")
                .replace(/\u200B/g, "")
                .replace(/\u00A0/g, " ")
                .trim();

            const hasBreakElement = element.innerHTML.trim() === "<br>";

            if (!textValue && !hasImage && !hasToken && !hasBreakElement) {
                element.remove();
            }
        });
    }

    /**
     * Prevents paragraphs from nesting inside paragraphs after pasted content is normalized.
     *
     * @param {HTMLElement} containerElement
     */
    function normalizeNestedParagraphs(containerElement) {
        Array.from(containerElement.querySelectorAll("p")).forEach(paragraphElement => {
            Array.from(paragraphElement.querySelectorAll("p")).forEach(nestedParagraphElement => {
                unwrapElementPreservingChildren(nestedParagraphElement);
            });
        });
    }
    

    /**
     * Interprets text that looks like a list item and returns its list metadata.
     *
     * @param {string} text
     * @returns {{ type: "ul" | "ol", content: string } | null}
     */
    function getListInfoFromText(text) {
        const trimmedText = (text || "").trim();

        if (!trimmedText) {
            return null;
        }

        const unorderedMatch = trimmedText.match(/^([•●▪◦\-*])\s+(.+)$/);
        if (unorderedMatch) {
            return {
                type: "ul",
                content: unorderedMatch[2]
            };
        }

        const orderedMatch = trimmedText.match(/^(\d+[.)])\s+(.+)$/);
        if (orderedMatch) {
            return {
                type: "ol",
                content: orderedMatch[2]
            };
        }

        return null;
    }

    /**
     * Converts pasted pseudo-lists such as "- item" paragraphs into real ul/ol structures.
     *
     * @param {HTMLElement} containerElement
     */
    function convertFakeListsToRealLists(containerElement) {
        const childNodes = Array.from(containerElement.childNodes);
        const newNodes = [];

        let currentListElement = null;
        let currentListType = null;

        function flushCurrentList() {
            if (!currentListElement) {
                return;
            }

            newNodes.push(currentListElement);
            currentListElement = null;
            currentListType = null;
        }

        childNodes.forEach(childNode => {
            if (
                childNode.nodeType === Node.ELEMENT_NODE &&
                ["p", "div"].includes(childNode.tagName.toLowerCase())
            ) {
                const textValue = childNode.textContent || "";
                const listInfo = getListInfoFromText(textValue);

                if (listInfo) {
                    if (!currentListElement || currentListType !== listInfo.type) {
                        flushCurrentList();
                        currentListElement = document.createElement(listInfo.type);
                        currentListType = listInfo.type;
                    }

                    const listItemElement = document.createElement("li");
                    listItemElement.innerHTML = escapeHtml(listInfo.content);
                    currentListElement.appendChild(listItemElement);
                    return;
                }
            }

            flushCurrentList();
            newNodes.push(childNode);
        });

        flushCurrentList();
        containerElement.replaceChildren(...newNodes);
    }

    /**
     * Creates an invisible marker element used to restore the selection after HTML cleanup.
     *
     * @param {string} markerId
     * @returns {HTMLSpanElement}
     */
    function createSelectionMarker(markerId) {
        const markerElement = document.createElement("span");
        markerElement.setAttribute(selectionMarkerAttributeName, markerId);
        markerElement.style.display = "inline-block";
        markerElement.style.width = "0";
        markerElement.style.height = "0";
        markerElement.style.overflow = "hidden";
        markerElement.textContent = zeroWidthSpaceCharacter;
        return markerElement;
    }

    /**
     * Saves the current selection by injecting temporary marker elements.
     * This approach survives innerHTML resets during normalization.
     *
     * @param {HTMLElement} editorElement
     * @returns {{ startMarkerId: string, endMarkerId: string } | null}
     */
    function saveSelectionWithMarkers(editorElement) {
        const selection = getSelectionInstance();

        if (!selection || selection.rangeCount === 0) {
            return null;
        }

        const range = selection.getRangeAt(0);

        if (!editorElement.contains(range.commonAncestorContainer)) {
            return null;
        }

        const uniqueIdSeed = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
        const startMarkerId = `start-${uniqueIdSeed}`;
        const endMarkerId = `end-${uniqueIdSeed}`;

        const startMarker = createSelectionMarker(startMarkerId);
        const endMarker = createSelectionMarker(endMarkerId);

        const endRange = range.cloneRange();
        endRange.collapse(false);
        endRange.insertNode(endMarker);

        const startRange = range.cloneRange();
        startRange.collapse(true);
        startRange.insertNode(startMarker);

        return { startMarkerId, endMarkerId };
    }

    /**
     * Restores the editor selection from previously inserted marker elements.
     *
     * @param {HTMLElement} editorElement
     * @param {{ startMarkerId: string, endMarkerId: string } | null} markerInfo
     */
    function restoreSelectionFromMarkers(editorElement, markerInfo) {
        if (!markerInfo) {
            return;
        }

        const startMarker = editorElement.querySelector(`[${selectionMarkerAttributeName}="${markerInfo.startMarkerId}"]`);
        const endMarker = editorElement.querySelector(`[${selectionMarkerAttributeName}="${markerInfo.endMarkerId}"]`);

        if (!startMarker || !endMarker) {
            clearSavedSelectionRange(editorElement);
            return;
        }

        const range = document.createRange();
        range.setStartAfter(startMarker);
        range.setEndBefore(endMarker);

        const selection = getSelectionInstance();
        selection.removeAllRanges();
        selection.addRange(range);
        saveSelectionRange(editorElement, range);

        startMarker.remove();
        endMarker.remove();
    }

    /**
     * Normalizes editor HTML into a consistent and safe structure.
     *
     * @param {HTMLElement} editorElement
     */
    function normalizeEditorHtml(editorElement) {
        if (!editorElement) {
            return;
        }

        const savedSelection = saveSelectionWithMarkers(editorElement);
        editorElement.innerHTML = sanitizeHtml(editorElement.innerHTML);

        normalizeTopLevelTextNodes(editorElement);
        convertFakeListsToRealLists(editorElement);
        normalizeNestedParagraphs(editorElement);
        removeEmptyInlineElements(editorElement);
        removeEmptyBlocks(editorElement);

        mergeAdjacentInlineElements(editorElement, "strong");
        mergeAdjacentInlineElements(editorElement, "em");
        mergeAdjacentInlineElements(editorElement, "u");

        if (!editorElement.innerHTML.trim()) {
            editorElement.innerHTML = emptyEditorHtml;
        }

        restoreSelectionFromMarkers(editorElement, savedSelection);
    }

    /**
     * Stores a full editor snapshot for undo and redo.
     *
     * @param {HTMLElement} editorElement
     */
    function pushHistorySnapshot(editorElement) {
        const state = getEditorState(editorElement);

        if (state.isApplyingHistory) {
            return;
        }

        const savedSelection = saveSelectionWithMarkers(editorElement);
        const snapshotHtml = editorElement.innerHTML;

        if (savedSelection) {
            restoreSelectionFromMarkers(editorElement, savedSelection);
        }

        const snapshot = {
            html: snapshotHtml,
            selection: savedSelection
                ? {
                    startMarkerId: savedSelection.startMarkerId,
                    endMarkerId: savedSelection.endMarkerId
                }
                : null
        };

        const currentSnapshot = state.history[state.historyIndex];
        if (currentSnapshot && currentSnapshot.html === snapshot.html) {
            return;
        }

        if (state.historyIndex < state.history.length - 1) {
            state.history = state.history.slice(0, state.historyIndex + 1);
        }

        state.history.push(snapshot);

        if (state.history.length > maxHistorySnapshots) {
            state.history.shift();
            state.historyIndex = state.history.length - 1;
            return;
        }

        state.historyIndex++;
    }

    /**
     * Debounces history creation so typing does not create a snapshot per keystroke.
     *
     * @param {HTMLElement} editorElement
     */
    function queueHistorySnapshot(editorElement) {
        const state = getEditorState(editorElement);

        if (state.isApplyingHistory) {
            return;
        }

        clearTimeout(state.inputDebounceTimeout);

        state.inputDebounceTimeout = setTimeout(() => {
            state.inputDebounceTimeout = null;
            pushHistorySnapshot(editorElement);
        }, historyDebounceDelayInMilliseconds);
    }
    /**
     * Applies a history snapshot and restores the saved caret/selection.
     *
     * @param {HTMLElement} editorElement
     * @param {{ html: string, selection: { startMarkerId: string, endMarkerId: string } | null }} snapshot
     */
    function applyHistorySnapshot(editorElement, snapshot) {
        const state = getEditorState(editorElement);

        state.isApplyingHistory = true;

        editorElement.innerHTML = snapshot.html || "";
        focusEditor(editorElement);

        if (snapshot.selection) {
            restoreSelectionFromMarkers(editorElement, snapshot.selection);
        } else {
            const range = document.createRange();
            range.selectNodeContents(editorElement);
            range.collapse(false);

            const selection = getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
            saveSelectionRange(editorElement, range);
        }

        state.isApplyingHistory = false;

        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);
    }

    /**
     * Debounces live preview updates while typing.
     *
     * @param {HTMLElement} editorElement
     */
    function queuePreviewUpdate(editorElement) {
        const state = getEditorState(editorElement);

        clearTimeout(state.previewDebounceTimeout);

        state.previewDebounceTimeout = setTimeout(() => {
            state.previewDebounceTimeout = null;
            notifyEditorChanged(editorElement);
        }, previewDebounceDelayInMilliseconds);
    }
    
    /**
     * Forces the latest typing state into history before undo/redo runs.
     */
    function flushPendingHistorySnapshot(editorElement) {
        const state = getEditorState(editorElement);

        if (state.inputDebounceTimeout) {
            clearTimeout(state.inputDebounceTimeout);
            state.inputDebounceTimeout = null;
            pushHistorySnapshot(editorElement);
        }
    }

    /**
     * @param {HTMLElement} editorElement
     */
    function undo(editorElement) {
        const state = getEditorState(editorElement);

        flushPendingHistorySnapshot(editorElement);

        if (state.historyIndex <= 0) {
            return;
        }

        state.historyIndex--;
        applyHistorySnapshot(editorElement, state.history[state.historyIndex]);
    }

    /**
     * @param {HTMLElement} editorElement
     */
    function redo(editorElement) {
        const state = getEditorState(editorElement);

        flushPendingHistorySnapshot(editorElement);

        if (state.historyIndex >= state.history.length - 1) {
            return;
        }

        state.historyIndex++;
        applyHistorySnapshot(editorElement, state.history[state.historyIndex]);
    }

    /**
     * Finds the nearest editable block that contains the current selection.
     *
     * @param {Range} range
     * @param {HTMLElement} editorElement
     * @returns {HTMLElement | null}
     */
    function findBlockElement(range, editorElement) {
        let blockElement = range.commonAncestorContainer;

        if (blockElement.nodeType === Node.TEXT_NODE) {
            blockElement = blockElement.parentNode;
        }

        while (blockElement && blockElement !== editorElement) {
            const tagName = blockElement.tagName?.toLowerCase();
            if (blockTagNames.has(tagName)) {
                return blockElement;
            }

            blockElement = blockElement.parentNode;
        }

        return null;
    }

    /**
     * Wraps or unwraps the current selection with an inline tag.
     *
     * @param {HTMLElement} editorElement
     * @param {string} tagName
     */
    function toggleInlineTag(editorElement, tagName) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range) {
            return;
        }

        const selection = getSelectionInstance();
        const startAncestor = getClosestAncestorTag(range.startContainer, tagName, editorElement);
        const endAncestor = getClosestAncestorTag(range.endContainer, tagName, editorElement);
        const commonAncestor =
            range.commonAncestorContainer.nodeType === Node.ELEMENT_NODE
                ? range.commonAncestorContainer
                : range.commonAncestorContainer.parentNode;

        const commonTagAncestor = getClosestAncestorTag(commonAncestor, tagName, editorElement);
        const elementToUnwrap = commonTagAncestor || (startAncestor && startAncestor === endAncestor ? startAncestor : null);

        // Toggle off
        if (elementToUnwrap) {
            const marker = document.createElement("span");
            marker.setAttribute(selectionMarkerAttributeName, `toggle-${Date.now()}-${Math.random().toString(36).slice(2)}`);
            marker.style.display = "inline-block";
            marker.style.width = "0";
            marker.style.height = "0";
            marker.style.overflow = "hidden";
            marker.textContent = zeroWidthSpaceCharacter;

            if (range.collapsed) {
                range.insertNode(marker);
            } else {
                const extractedContents = range.extractContents();
                const fragment = document.createDocumentFragment();
                fragment.appendChild(extractedContents);
                fragment.appendChild(marker);
                range.insertNode(fragment);
            }

            unwrapElement(elementToUnwrap);

            const restoredMarker = editorElement.querySelector(
                `[${selectionMarkerAttributeName}="${marker.getAttribute(selectionMarkerAttributeName)}"]`
            );

            if (restoredMarker) {
                const newRange = document.createRange();
                newRange.setStartBefore(restoredMarker);
                newRange.collapse(true);

                selection.removeAllRanges();
                selection.addRange(newRange);

                restoredMarker.remove();
                saveSelectionRange(editorElement, newRange);
            }

            pushHistorySnapshot(editorElement);
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        // Toggle on for collapsed caret
        if (range.collapsed) {
            const wrapperElement = document.createElement(tagName);
            wrapperElement.appendChild(document.createTextNode(zeroWidthSpaceCharacter));
            range.insertNode(wrapperElement);

            const newRange = document.createRange();
            newRange.setStart(wrapperElement.firstChild, 1);
            newRange.collapse(true);

            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);

            pushHistorySnapshot(editorElement);
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        // Toggle on for selection
        const wrapperElement = document.createElement(tagName);
        wrapperElement.appendChild(range.extractContents());
        range.insertNode(wrapperElement);

        const newRange = document.createRange();
        newRange.selectNodeContents(wrapperElement);
        selection.removeAllRanges();
        selection.addRange(newRange);
        saveSelectionRange(editorElement, newRange);

        pushHistorySnapshot(editorElement);
        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);
    }

    /**
     * Applies a block-level tag to the current selection.
     * If part of a paragraph is selected, the paragraph is split so only the selected
     * content becomes the requested block element.
     *
     * @param {HTMLElement} editorElement
     * @param {string} tagName
     */
    function applyBlockTag(editorElement, tagName) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range) {
            return;
        }

        const selection = getSelectionInstance();
        const targetTagName = (tagName || "p").toLowerCase();
        const blockElement = findBlockElement(range, editorElement);

        if (range.collapsed) {
            if (!blockElement || blockElement === editorElement) {
                const newBlockElement = document.createElement(targetTagName);
                newBlockElement.innerHTML = "<br>";
                range.insertNode(newBlockElement);
                placeCaretInsideElement(newBlockElement);
                pushHistorySnapshot(editorElement);
                return;
            }

            const currentTagName = blockElement.tagName.toLowerCase();
            if (currentTagName === targetTagName) {
                return;
            }

            const replacementElement = document.createElement(targetTagName);
            replacementElement.innerHTML = blockElement.innerHTML;
            blockElement.replaceWith(replacementElement);

            placeCaretInsideElement(replacementElement);
            pushHistorySnapshot(editorElement);
            return;
        }

        const selectedText = (range.toString() || "").trim();
        if (!selectedText) {
            return;
        }

        // If the selection covers only part of a normal text block, split that block.
        if (
            blockElement &&
            blockElement !== editorElement &&
            editableBlockTagNames.has(blockElement.tagName.toLowerCase()) &&
            blockElement.tagName.toLowerCase() !== "blockquote"
        ) {
            const fullBlockRange = document.createRange();
            fullBlockRange.selectNodeContents(blockElement);

            const startsAtBlockStart =
                range.startContainer === fullBlockRange.startContainer &&
                range.startOffset === fullBlockRange.startOffset;

            const endsAtBlockEnd =
                range.endContainer === fullBlockRange.endContainer &&
                range.endOffset === fullBlockRange.endOffset;

            if (!startsAtBlockStart || !endsAtBlockEnd) {
                const beforeRange = document.createRange();
                beforeRange.setStart(fullBlockRange.startContainer, fullBlockRange.startOffset);
                beforeRange.setEnd(range.startContainer, range.startOffset);

                const selectedRange = range.cloneRange();

                const afterRange = document.createRange();
                afterRange.setStart(range.endContainer, range.endOffset);
                afterRange.setEnd(fullBlockRange.endContainer, fullBlockRange.endOffset);

                const beforeFragment = beforeRange.extractContents();
                const selectedFragment = selectedRange.extractContents();
                const afterFragment = afterRange.extractContents();

                const fragment = document.createDocumentFragment();

                const beforeText = beforeFragment.textContent?.replace(/\u200B/g, "").trim();
                if (beforeText || beforeFragment.querySelector?.("img, .mail-merge-token")) {
                    const beforeBlockElement = document.createElement("p");
                    beforeBlockElement.appendChild(beforeFragment);
                    fragment.appendChild(beforeBlockElement);
                }

                const middleBlockElement = document.createElement(targetTagName);
                if (selectedFragment.childNodes.length === 0) {
                    middleBlockElement.innerHTML = "<br>";
                } else {
                    middleBlockElement.appendChild(selectedFragment);
                }

                const caretMarkerId = `block-${Date.now()}-${Math.random().toString(36).slice(2)}`;
                middleBlockElement.appendChild(createSelectionMarker(caretMarkerId));
                fragment.appendChild(middleBlockElement);

                const afterText = afterFragment.textContent?.replace(/\u200B/g, "").trim();
                if (afterText || afterFragment.querySelector?.("img, .mail-merge-token")) {
                    const afterBlockElement = document.createElement("p");
                    afterBlockElement.appendChild(afterFragment);
                    fragment.appendChild(afterBlockElement);
                }

                blockElement.replaceWith(fragment);

                const restoredMarker = editorElement.querySelector(
                    `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
                );

                if (restoredMarker) {
                    const newRange = document.createRange();
                    newRange.setStartBefore(restoredMarker);
                    newRange.collapse(true);

                    selection.removeAllRanges();
                    selection.addRange(newRange);
                    saveSelectionRange(editorElement, newRange);

                    restoredMarker.remove();
                }

                pushHistorySnapshot(editorElement);
                return;
            }
        }

        // If full block selected, just convert the whole block.
        if (blockElement && blockElement !== editorElement) {
            const currentTagName = blockElement.tagName.toLowerCase();

            if (currentTagName === targetTagName) {
                return;
            }

            const caretMarkerId = `block-${Date.now()}-${Math.random().toString(36).slice(2)}`;
            blockElement.appendChild(createSelectionMarker(caretMarkerId));

            const replacementElement = document.createElement(targetTagName);
            replacementElement.innerHTML = blockElement.innerHTML;
            blockElement.replaceWith(replacementElement);

            const restoredMarker = editorElement.querySelector(
                `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
            );

            if (restoredMarker) {
                const newRange = document.createRange();
                newRange.setStartBefore(restoredMarker);
                newRange.collapse(true);

                selection.removeAllRanges();
                selection.addRange(newRange);
                saveSelectionRange(editorElement, newRange);

                restoredMarker.remove();
            } else {
                placeCaretInsideElement(replacementElement);
            }

            pushHistorySnapshot(editorElement);
            return;
        }

        // Fallback
        const extractedContents = range.extractContents();
        const newBlockElement = document.createElement(targetTagName);

        if (extractedContents.childNodes.length === 0) {
            newBlockElement.innerHTML = "<br>";
        } else {
            newBlockElement.appendChild(extractedContents);
        }

        const fallbackMarkerId = `block-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        newBlockElement.appendChild(createSelectionMarker(fallbackMarkerId));
        range.insertNode(newBlockElement);

        const restoredMarker = editorElement.querySelector(
            `[${selectionMarkerAttributeName}="${fallbackMarkerId}"]`
        );

        if (restoredMarker) {
            const newRange = document.createRange();
            newRange.setStartBefore(restoredMarker);
            newRange.collapse(true);

            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);

            restoredMarker.remove();
        } else {
            placeCaretInsideElement(newBlockElement);
        }

        pushHistorySnapshot(editorElement);
    }

    function getCurrentBlockTag(editorElement) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return "p";
        }

        const blockElement = findBlockElement(range, editorElement);
        if (!blockElement || blockElement === editorElement) {
            return "p";
        }

        return blockElement.tagName.toLowerCase();
    }

    function getCurrentTextColor(editorElement) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return "";
        }

        const coloredSpanElement =
            getClosestAncestorTag(range.startContainer, "span", editorElement) ||
            getClosestAncestorTag(range.endContainer, "span", editorElement);

        if (!coloredSpanElement || !coloredSpanElement.classList.contains("mail-text-color")) {
            return "";
        }

        return coloredSpanElement.getAttribute("data-text-color") || "";
    }

    function getCurrentLinkData(editorElement) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return { url: "", text: "" };
        }

        const linkElement =
            getClosestAncestorTag(range.startContainer, "a", editorElement) ||
            getClosestAncestorTag(range.endContainer, "a", editorElement);

        if (!linkElement) {
            return { url: "", text: "" };
        }

        return {
            url: linkElement.getAttribute("href") || "",
            text: linkElement.textContent || ""
        };
    }

    function stripInlineFormattingFromNode(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            return document.createTextNode(node.textContent || "");
        }

        if (node.nodeType !== Node.ELEMENT_NODE) {
            return document.createDocumentFragment();
        }

        const sourceElement = /** @type {HTMLElement} */ (node);
        const tagName = sourceElement.tagName.toLowerCase();

        if (sourceElement.classList.contains("mail-merge-token")) {
            return sourceElement.cloneNode(true);
        }

        if (
            tagName === "strong" || tagName === "b" ||
            tagName === "em" || tagName === "i" ||
            tagName === "u" ||
            tagName === "a" ||
            (tagName === "span" && sourceElement.classList.contains("mail-text-color"))
        ) {
            const fragment = document.createDocumentFragment();

            Array.from(sourceElement.childNodes).forEach(childNode => {
                fragment.appendChild(stripInlineFormattingFromNode(childNode));
            });

            return fragment;
        }

        const clonedElement = document.createElement(tagName);

        if (tagName === "img") {
            const sourceValue = sanitizeUrl(sourceElement.getAttribute("src"));
            if (!sourceValue) {
                return document.createDocumentFragment();
            }

            clonedElement.setAttribute("src", sourceValue);
            clonedElement.setAttribute("alt", sourceElement.getAttribute("alt") || "");
            return clonedElement;
        }

        Array.from(sourceElement.childNodes).forEach(childNode => {
            clonedElement.appendChild(stripInlineFormattingFromNode(childNode));
        });

        return clonedElement;
    }

    function readFileAsDataUrl(file) {
        return new Promise((resolve, reject) => {
            const fileReader = new FileReader();
            fileReader.onload = () => resolve(fileReader.result);
            fileReader.onerror = reject;
            fileReader.readAsDataURL(file);
        });
    }
    
    /**
     * Toggles the current selection into or out of a list.
     *
     * @param {HTMLElement} editorElement
     * @param {"ul" | "ol"} listTagName
     */
    function toggleList(editorElement, listTagName) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range) {
            return;
        }

        const selection = getSelectionInstance();
        const existingList = getClosestAncestorByTags(range.commonAncestorContainer, ["ul", "ol"], editorElement);

        if (existingList && existingList.tagName.toLowerCase() === listTagName.toLowerCase()) {
            const caretMarkerId = `list-toggle-off-${Date.now()}-${Math.random().toString(36).slice(2)}`;
            const fragment = document.createDocumentFragment();

            Array.from(existingList.children).forEach((listItemElement, index) => {
                const paragraphElement = document.createElement("p");
                paragraphElement.innerHTML = listItemElement.innerHTML.trim() || "<br>";

                if (index === existingList.children.length - 1) {
                    paragraphElement.appendChild(createSelectionMarker(caretMarkerId));
                }

                fragment.appendChild(paragraphElement);
            });

            existingList.replaceWith(fragment);

            const restoredMarker = editorElement.querySelector(
                `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
            );

            if (restoredMarker) {
                const newRange = document.createRange();
                newRange.setStartBefore(restoredMarker);
                newRange.collapse(true);
                selection.removeAllRanges();
                selection.addRange(newRange);
                saveSelectionRange(editorElement, newRange);
                restoredMarker.remove();
            }

            pushHistorySnapshot(editorElement);
            return;
        }

        if (existingList && existingList.tagName.toLowerCase() !== listTagName.toLowerCase()) {
            const replacementList = document.createElement(listTagName);
            replacementList.innerHTML = existingList.innerHTML;
            existingList.replaceWith(replacementList);
            pushHistorySnapshot(editorElement);
            return;
        }

        if (range.collapsed) {
            const listElement = document.createElement(listTagName);
            const listItemElement = document.createElement("li");
            listItemElement.innerHTML = "<br>";
            listElement.appendChild(listItemElement);

            const currentBlockElement = findBlockElement(range, editorElement);

            if (currentBlockElement && currentBlockElement !== editorElement) {
                currentBlockElement.replaceWith(listElement);
            } else {
                range.insertNode(listElement);
            }
            
            placeCaretInsideElement(listElement.querySelector("li"));
            pushHistorySnapshot(editorElement);
            return;
        }

        const extractedContents = range.extractContents();
        const listElement = document.createElement(listTagName);
        const topLevelNodes = Array.from(extractedContents.childNodes).filter(node => {
            if (node.nodeType === Node.TEXT_NODE) {
                return (node.textContent || "").trim().length > 0;
            }

            return true;
        });

        let createdListItemCount = 0;

        topLevelNodes.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE) {
                const currentTagName = node.tagName.toLowerCase();

                if (editableBlockTagNames.has(currentTagName)) {
                    const listItemElement = document.createElement("li");
                    listItemElement.innerHTML = node.innerHTML.trim() || "<br>";
                    listElement.appendChild(listItemElement);
                    createdListItemCount += 1;
                    return;
                }

                if (currentTagName === "ul" || currentTagName === "ol") {
                    Array.from(node.children).forEach(nestedListItem => {
                        if (nestedListItem.tagName?.toLowerCase() === "li") {
                            const listItemElement = document.createElement("li");
                            listItemElement.innerHTML = nestedListItem.innerHTML.trim() || "<br>";
                            listElement.appendChild(listItemElement);
                            createdListItemCount += 1;
                        }
                    });
                    return;
                }
            }

            const listItemElement = document.createElement("li");

            if (node.nodeType === Node.TEXT_NODE) {
                listItemElement.textContent = (node.textContent || "").trim() || zeroWidthSpaceCharacter;
            } else {
                const temporaryContainer = document.createElement("div");
                temporaryContainer.appendChild(node.cloneNode(true));
                listItemElement.innerHTML = temporaryContainer.innerHTML.trim() || "<br>";
            }

            listElement.appendChild(listItemElement);
            createdListItemCount += 1;
        });

        if (createdListItemCount === 0) {
            const listItemElement = document.createElement("li");
            listItemElement.innerHTML = "<br>";
            listElement.appendChild(listItemElement);
        }

        const caretMarkerId = `list-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        listElement.appendChild(createSelectionMarker(caretMarkerId));
        range.insertNode(listElement);

        const restoredMarker = editorElement.querySelector(
            `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
        );

        if (restoredMarker) {
            const newRange = document.createRange();
            newRange.setStartBefore(restoredMarker);
            newRange.collapse(true);
            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);
            restoredMarker.remove();
        }

        pushHistorySnapshot(editorElement);
    }

    /**
     * Inserts sanitized HTML at the current caret position.
     *
     * @param {HTMLElement} editorElement
     * @param {string} html
     */
    function insertHtmlAtCursor(editorElement, html) {
        focusEditor(editorElement);

        let range = getLiveSelectionRange(editorElement) || getSelectionRange(editorElement);

        if (!range) {
            const rangeAtEnd = document.createRange();
            rangeAtEnd.selectNodeContents(editorElement);
            rangeAtEnd.collapse(false);

            const selection = getSelectionInstance();
            selection.removeAllRanges();
            selection.addRange(rangeAtEnd);

            range = rangeAtEnd;
            saveSelectionRange(editorElement, rangeAtEnd);
        }

        range.deleteContents();

        const temporaryContainer = document.createElement("div");
        temporaryContainer.innerHTML = html;

        const fragment = document.createDocumentFragment();
        let currentNode = null;

        while ((currentNode = temporaryContainer.firstChild)) {
            fragment.appendChild(currentNode);
        }

        const caretMarkerId = `insert-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        fragment.appendChild(createSelectionMarker(caretMarkerId));

        range.insertNode(fragment);

        const restoredMarker = editorElement.querySelector(
            `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
        );

        if (restoredMarker) {
            const selection = getSelectionInstance();
            const newRange = document.createRange();
            newRange.setStartBefore(restoredMarker);
            newRange.collapse(true);

            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);

            restoredMarker.remove();
        }

        pushHistorySnapshot(editorElement);
    }

    /**
     * Inserts a hyperlink at the current selection.
     *
     * @param {HTMLElement} editorElement
     * @param {string} url
     * @param {string} [linkText]
     */
    function insertOrUpdateLink(editorElement, url, linkText) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range || !url) {
            return;
        }

        const normalizedLinkText = (linkText || "").trim();
        const existingLinkElement =
            getClosestAncestorTag(range.startContainer, "a", editorElement) ||
            getClosestAncestorTag(range.endContainer, "a", editorElement);

        if (existingLinkElement) {
            existingLinkElement.setAttribute("href", url);
            existingLinkElement.setAttribute("target", "_blank");
            existingLinkElement.setAttribute("rel", "noopener noreferrer");

            if (normalizedLinkText) {
                existingLinkElement.textContent = normalizedLinkText;
            }
            
            pushHistorySnapshot(editorElement);
            return;
        }

        const anchorElement = document.createElement("a");
        anchorElement.href = url;
        anchorElement.target = "_blank";
        anchorElement.rel = "noopener noreferrer";

        if (range.collapsed) {
            anchorElement.textContent = normalizedLinkText || url;
            range.insertNode(anchorElement);

            const newRange = document.createRange();
            newRange.setStartAfter(anchorElement);
            newRange.collapse(true);

            const selection = getSelectionInstance();
            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);
            
            pushHistorySnapshot(editorElement);
            return;
        }

        if (normalizedLinkText) {
            anchorElement.textContent = normalizedLinkText;
        } else {
            anchorElement.appendChild(range.extractContents());
        }

        range.deleteContents();
        range.insertNode(anchorElement);

        const newRange = document.createRange();
        newRange.setStartAfter(anchorElement);
        newRange.collapse(true);

        const selection = getSelectionInstance();
        selection.removeAllRanges();
        selection.addRange(newRange);
        saveSelectionRange(editorElement, newRange);
        
        pushHistorySnapshot(editorElement);
    }

    /**
     * Removes the closest link around the current selection.
     *
     * @param {HTMLElement} editorElement
     */
    function removeLink(editorElement) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range) {
            return;
        }

        const linkElement =
            getClosestAncestorTag(range.startContainer, "a", editorElement) ||
            getClosestAncestorTag(range.endContainer, "a", editorElement);

        if (!linkElement) {
            return;
        }

        unwrapElement(linkElement);
        pushHistorySnapshot(editorElement);
    }

    /**
     * @param {HTMLElement} editorElement
     * @param {string} tagName
     * @returns {boolean}
     */
    function isTagActive(editorElement, tagName) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return false;
        }

        const startAncestor = getClosestAncestorTag(range.startContainer, tagName, editorElement);
        const endAncestor = getClosestAncestorTag(range.endContainer, tagName, editorElement);

        return !!startAncestor && !!endAncestor && startAncestor === endAncestor;
    }

    /**
     * @param {HTMLElement} editorElement
     * @param {"ul" | "ol"} listTagName
     * @returns {boolean}
     */
    function isListActive(editorElement, listTagName) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return false;
        }

        const listElement = getClosestAncestorByTags(range.commonAncestorContainer, ["ul", "ol"], editorElement);
        return !!listElement && listElement.tagName.toLowerCase() === listTagName.toLowerCase();
    }

    /**
     * @param {HTMLElement} editorElement
     * @returns {boolean}
     */
    function isLinkActive(editorElement) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return false;
        }

        return !!getClosestAncestorTag(range.startContainer, "a", editorElement);
    }

    /**
     * @param {HTMLElement} editorElement
     * @returns {boolean}
     */
    function isSelectionInsideList(editorElement) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return false;
        }

        return !!getClosestAncestorTag(range.startContainer, "li", editorElement);
    }

    /**
     * @param {HTMLElement} listItemElement
     * @returns {boolean}
     */
    function isListItemVisuallyEmpty(listItemElement) {
        if (!listItemElement) {
            return false;
        }

        const textContent = (listItemElement.textContent || "")
            .replace(/\u200B/g, "")
            .replace(/\u00A0/g, " ")
            .trim();

        const hasMeaningfulText = textContent.length > 0;
        const hasImage = !!listItemElement.querySelector("img");
        const hasToken = !!listItemElement.querySelector(".mail-merge-token");
        const hasBreakOnly =
            listItemElement.innerHTML.trim() === "<br>" ||
            listItemElement.innerHTML.trim() === "";

        return (!hasMeaningfulText && !hasImage && !hasToken) || hasBreakOnly;
    }

    function indentCurrentListItem(editorElement, currentListItemElement = null) {
        const range = getSelectionRange(editorElement);
        const listItemElement =
            currentListItemElement ||
            (range
                ? getClosestAncestorTag(range.startContainer, "li", editorElement) ||
                getClosestAncestorTag(range.endContainer, "li", editorElement)
                : null);

        if (!listItemElement) {
            return false;
        }

        const previousListItemElement = listItemElement.previousElementSibling;
        if (!previousListItemElement || previousListItemElement.tagName?.toLowerCase() !== "li") {
            return false;
        }

        const parentListElement = getClosestAncestorByTags(listItemElement, ["ul", "ol"], editorElement);
        if (!parentListElement) {
            return false;
        }

        const caretMarkerId = `indent-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        listItemElement.insertBefore(createSelectionMarker(caretMarkerId), listItemElement.firstChild);

        let nestedListElement = Array.from(previousListItemElement.children).find(childElement =>
            childElement.tagName?.toLowerCase() === parentListElement.tagName.toLowerCase());

        if (!nestedListElement) {
            nestedListElement = document.createElement(parentListElement.tagName.toLowerCase());
            previousListItemElement.appendChild(nestedListElement);
        }

        nestedListElement.appendChild(listItemElement);

        const restoredMarker = editorElement.querySelector(
            `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
        );

        if (restoredMarker) {
            const newRange = document.createRange();
            newRange.setStartBefore(restoredMarker);
            newRange.collapse(true);

            const selection = getSelectionInstance();
            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);

            restoredMarker.remove();
        }

        pushHistorySnapshot(editorElement);
        return true;
    }

    function outdentCurrentListItem(editorElement, currentListItemElement = null) {
        const range = getSelectionRange(editorElement);
        const listItemElement =
            currentListItemElement ||
            (range
                ? getClosestAncestorTag(range.startContainer, "li", editorElement) ||
                getClosestAncestorTag(range.endContainer, "li", editorElement)
                : null);

        if (!listItemElement) {
            return false;
        }

        const parentListElement = getClosestAncestorByTags(listItemElement, ["ul", "ol"], editorElement);
        if (!parentListElement) {
            return false;
        }

        const parentListItemElement = getClosestAncestorTag(parentListElement.parentNode, "li", editorElement);

        const caretMarkerId = `outdent-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        listItemElement.insertBefore(createSelectionMarker(caretMarkerId), listItemElement.firstChild);

        if (!parentListItemElement) {
            const paragraphElement = document.createElement("p");
            paragraphElement.innerHTML = listItemElement.innerHTML.trim() || "<br>";
            parentListElement.parentNode.insertBefore(paragraphElement, parentListElement.nextSibling);
            listItemElement.remove();

            if (parentListElement.children.length === 0) {
                parentListElement.remove();
            }
        } else {
            parentListItemElement.parentNode.insertBefore(listItemElement, parentListItemElement.nextSibling);

            if (parentListElement.children.length === 0) {
                parentListElement.remove();
            }
        }

        const restoredMarker = editorElement.querySelector(
            `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
        );

        if (restoredMarker) {
            const newRange = document.createRange();
            newRange.setStartBefore(restoredMarker);
            newRange.collapse(true);

            const selection = getSelectionInstance();
            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);

            restoredMarker.remove();
        }

        pushHistorySnapshot(editorElement);
        return true;
    }

    /**
     * Leaves the current list when the active list item is visually empty.
     *
     * @param {HTMLElement} editorElement
     * @returns {boolean}
     */
    function exitListAtCurrentItem(editorElement) {
        const range = getSelectionRange(editorElement);
        if (!range) {
            return false;
        }

        const listItemElement = getClosestAncestorTag(range.startContainer, "li", editorElement);
        if (!listItemElement || !isListItemVisuallyEmpty(listItemElement)) {
            return false;
        }

        const listElement = getClosestAncestorByTags(listItemElement, ["ul", "ol"], editorElement);
        if (!listElement) {
            return false;
        }

        const paragraphElement = document.createElement("p");
        paragraphElement.appendChild(document.createElement("br"));

        listItemElement.remove();

        if (listElement.children.length === 0) {
            listElement.replaceWith(paragraphElement);
        } else {
            listElement.after(paragraphElement);
        }

        placeCaretInsideElement(paragraphElement);
        pushHistorySnapshot(editorElement);

        return true;
    }

    async function tryInsertImageFromFile(editorElement, file) {
        if (!file || !file.type.startsWith("image/")) {
            return false;
        }

        const dataUrl = await readFileAsDataUrl(file);
        const imageHtml = `<img src="${escapeHtml(String(dataUrl))}" alt="">`;

        insertHtmlAtCursor(editorElement, imageHtml);
        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);

        return true;
    }

    /**
     * Inserts a hard line break instead of creating a new paragraph.
     *
     * @param {HTMLElement} editorElement
     */
    function insertLineBreakAtCursor(editorElement) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);

        if (!range) {
            return;
        }

        range.deleteContents();

        const breakElement = document.createElement("br");
        range.insertNode(breakElement);

        const newRange = document.createRange();
        newRange.setStartAfter(breakElement);
        newRange.collapse(true);

        const selection = getSelection();
        selection.removeAllRanges();
        selection.addRange(newRange);

        pushHistorySnapshot(editorElement);
        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);
    }

    function insertTabSpacesAtCursor(editorElement) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range) {
            return;
        }

        range.deleteContents();

        const textNode = document.createTextNode("\u00A0\u00A0\u00A0\u00A0");
        range.insertNode(textNode);

        const newRange = document.createRange();
        newRange.setStartAfter(textNode);
        newRange.collapse(true);

        const selection = getSelectionInstance();
        selection.removeAllRanges();
        selection.addRange(newRange);
        saveSelectionRange(editorElement, newRange);

        pushHistorySnapshot(editorElement);
    }

    /**
     * Inserts a new paragraph near the current block position.
     *
     * @param {HTMLElement} editorElement
     */
    function insertParagraphAtCursor(editorElement) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);

        if (!range) {
            return;
        }

        range.deleteContents();

        const paragraphElement = document.createElement("p");
        paragraphElement.appendChild(document.createElement("br"));

        const currentBlockElement = findBlockElement(range, editorElement);

        if (currentBlockElement && currentBlockElement !== editorElement) {
            currentBlockElement.after(paragraphElement);
        } else {
            range.insertNode(paragraphElement);
        }

        placeCaretInsideElement(paragraphElement);
        pushHistorySnapshot(editorElement);
        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);
    }

    /**
     * Reports which formatting options are currently active at the selection.
     *
     * @param {HTMLElement} editorElement
     * @returns {{ bold: boolean, italic: boolean, underline: boolean, unorderedList: boolean, orderedList: boolean, link: boolean }}
     */
    function getActiveFormats(editorElement) {
        const currentLinkData = getCurrentLinkData(editorElement);

        return {
            bold: isTagActive(editorElement, "strong") || isTagActive(editorElement, "b"),
            italic: isTagActive(editorElement, "em") || isTagActive(editorElement, "i"),
            underline: isTagActive(editorElement, "u"),
            unorderedList: isListActive(editorElement, "ul"),
            orderedList: isListActive(editorElement, "ol"),
            link: isLinkActive(editorElement),
            currentBlockTag: getCurrentBlockTag(editorElement),
            currentTextColor: getCurrentTextColor(editorElement),
            currentLinkUrl: currentLinkData.url,
            currentLinkText: currentLinkData.text
        };
    }
    function clearFormatting(editorElement) {
        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range) {
            return;
        }

        const selection = getSelectionInstance();

        if (range.collapsed) {
            const inlineAncestorCandidates = [
                getClosestAncestorTag(range.startContainer, "strong", editorElement),
                getClosestAncestorTag(range.startContainer, "b", editorElement),
                getClosestAncestorTag(range.startContainer, "em", editorElement),
                getClosestAncestorTag(range.startContainer, "i", editorElement),
                getClosestAncestorTag(range.startContainer, "u", editorElement),
                getClosestAncestorTag(range.startContainer, "a", editorElement),
                getClosestAncestorTag(range.startContainer, "span", editorElement)
            ].filter(Boolean);

            const coloredSpanElement = inlineAncestorCandidates.find(element =>
                element.tagName.toLowerCase() === "span" && element.classList.contains("mail-text-color"));

            const elementToUnwrap = coloredSpanElement || inlineAncestorCandidates[0];

            if (elementToUnwrap) {
                unwrapElement(elementToUnwrap);
                normalizeEditorHtml(editorElement);
                pushHistorySnapshot(editorElement);
            }

            return;
        }

        const extractedContents = range.extractContents();
        const cleanedFragment = document.createDocumentFragment();

        Array.from(extractedContents.childNodes).forEach(childNode => {
            cleanedFragment.appendChild(stripInlineFormattingFromNode(childNode));
        });

        const caretMarkerId = `clear-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        cleanedFragment.appendChild(createSelectionMarker(caretMarkerId));
        range.insertNode(cleanedFragment);

        normalizeEditorHtml(editorElement);

        const restoredMarker = editorElement.querySelector(
            `[${selectionMarkerAttributeName}="${caretMarkerId}"]`
        );

        if (restoredMarker) {
            const newRange = document.createRange();
            newRange.setStartBefore(restoredMarker);
            newRange.collapse(true);

            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);

            restoredMarker.remove();
        }

        pushHistorySnapshot(editorElement);
    }

    function applyTextColor(editorElement, colorValue) {
        focusEditor(editorElement);

        if (!/^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(colorValue || "")) {
            return;
        }

        const range = getSelectionRange(editorElement);
        if (!range) {
            return;
        }

        const selection = getSelectionInstance();

        if (range.collapsed) {
            const wrapperElement = document.createElement("span");
            wrapperElement.setAttribute("class", "mail-text-color");
            wrapperElement.setAttribute("data-text-color", colorValue);
            wrapperElement.style.color = colorValue;
            wrapperElement.appendChild(document.createTextNode(zeroWidthSpaceCharacter));
            range.insertNode(wrapperElement);

            const newRange = document.createRange();
            newRange.setStart(wrapperElement.firstChild, 1);
            newRange.collapse(true);

            selection.removeAllRanges();
            selection.addRange(newRange);
            saveSelectionRange(editorElement, newRange);

            pushHistorySnapshot(editorElement);
            return;
        }

        const wrapperElement = document.createElement("span");
        wrapperElement.setAttribute("class", "mail-text-color");
        wrapperElement.setAttribute("data-text-color", colorValue);
        wrapperElement.style.color = colorValue;

        const extractedContents = range.extractContents();
        Array.from(extractedContents.childNodes).forEach(childNode => {
            wrapperElement.appendChild(stripInlineFormattingFromNode(childNode));
        });

        range.insertNode(wrapperElement);

        const newRange = document.createRange();
        newRange.selectNodeContents(wrapperElement);
        selection.removeAllRanges();
        selection.addRange(newRange);
        saveSelectionRange(editorElement, newRange);
        
        pushHistorySnapshot(editorElement);
    }

    /**
     * Handles keyboard behavior that contenteditable does not do consistently across browsers.
     *
     * @param {KeyboardEvent} event
     * @param {HTMLElement} editorElement
     */
    function handleKeyboardShortcuts(event, editorElement) {
        const range = getSelectionRange(editorElement);

        if (range) {
            const listItemElement = getClosestAncestorTag(range.startContainer, "li", editorElement);

            if (listItemElement && isListItemVisuallyEmpty(listItemElement)) {
                if (event.key === "Backspace") {
                    event.preventDefault();

                    const parentListElement = getClosestAncestorByTags(listItemElement, ["ul", "ol"], editorElement);
                    const parentListItemElement = parentListElement
                        ? getClosestAncestorTag(parentListElement.parentNode, "li", editorElement)
                        : null;

                    if (parentListItemElement) {
                        outdentCurrentListItem(editorElement, listItemElement);
                    } else {
                        exitListAtCurrentItem(editorElement);
                    }

                    notifyEditorChanged(editorElement);
                    notifySelectionChanged(editorElement);
                    return;
                }

                if (event.key === "Enter") {
                    event.preventDefault();
                    exitListAtCurrentItem(editorElement);
                    notifyEditorChanged(editorElement);
                    notifySelectionChanged(editorElement);
                    return;
                }
            }
        }

        if (event.key === "Enter" && !event.shiftKey) {
            if (range && !isSelectionInsideList(editorElement)) {
                event.preventDefault();
                insertParagraphAtCursor(editorElement);
                return;
            }
        }

        if (event.key === "Enter" && event.shiftKey) {
            if (range && !isSelectionInsideList(editorElement)) {
                event.preventDefault();
                insertLineBreakAtCursor(editorElement);
                return;
            }
        }

        const isModifierPressed = event.ctrlKey || event.metaKey;
        if (!isModifierPressed) {
            return;
        }

        const key = event.key.toLowerCase();

        if (key === "z" && !event.shiftKey) {
            event.preventDefault();
            flushPendingHistorySnapshot(editorElement);
            undo(editorElement);
            return;
        }

        if (key === "y" || (key === "z" && event.shiftKey)) {
            event.preventDefault();
            flushPendingHistorySnapshot(editorElement);
            redo(editorElement);
            return;
        }

        if (key === "b") {
            event.preventDefault();
            toggleInlineTag(editorElement, "strong");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (key === "i") {
            event.preventDefault();
            toggleInlineTag(editorElement, "em");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (key === "u") {
            event.preventDefault();
            toggleInlineTag(editorElement, "u");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
        }
        
        if (key === "7" && event.shiftKey) {
            event.preventDefault();
            toggleList(editorElement, "ol");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (key === "8" && event.shiftKey) {
            event.preventDefault();
            toggleList(editorElement, "ul");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (key === "\\") {
            event.preventDefault();
            clearFormatting(editorElement);
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (event.altKey && key === "1") {
            event.preventDefault();
            applyBlockTag(editorElement, "h1");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (event.altKey && key === "2") {
            event.preventDefault();
            applyBlockTag(editorElement, "h2");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (event.altKey && key === "3") {
            event.preventDefault();
            applyBlockTag(editorElement, "h3");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
            return;
        }

        if (event.altKey && key === "0") {
            event.preventDefault();
            applyBlockTag(editorElement, "p");
            notifyEditorChanged(editorElement);
            notifySelectionChanged(editorElement);
        }
    }

    /**
     * Debounces structural cleanup after native contenteditable mutations.
     *
     * @param {HTMLElement} editorElement
     */
    function queueCleanup(editorElement) {
        const state = getEditorState(editorElement);

        clearTimeout(state.cleanupDebounceTimeout);

        state.cleanupDebounceTimeout = setTimeout(() => {
            if (state.isApplyingHistory) {
                return;
            }

            normalizeEditorHtml(editorElement);
        }, cleanupDebounceDelayInMilliseconds);
    }

    /**
     * Handles pasting HTML or plain text into the editor.
     *
     * @param {ClipboardEvent} event
     * @param {HTMLElement} editorElement
     */
    async function handlePaste(event, editorElement) {
        const clipboardData = event.clipboardData;
        if (!clipboardData) {
            return;
        }

        const imageFile = Array.from(clipboardData.items || [])
            .find(item => item.type && item.type.startsWith("image/"))
            ?.getAsFile();

        if (imageFile) {
            event.preventDefault();
            await tryInsertImageFromFile(editorElement, imageFile);
            return;
        }

        event.preventDefault();

        const pastedHtml = clipboardData.getData("text/html");
        const pastedPlainText = clipboardData.getData("text/plain");

        let htmlToInsert = "";

        if (pastedHtml && pastedHtml.trim()) {
            htmlToInsert = sanitizeHtml(pastedHtml);
        } else if (pastedPlainText && pastedPlainText.trim()) {
            htmlToInsert = convertPlainTextToHtml(pastedPlainText);
        }

        if (!htmlToInsert) {
            return;
        }

        insertHtmlAtCursor(editorElement, htmlToInsert);
        normalizeEditorHtml(editorElement);
        pushHistorySnapshot(editorElement);
        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);
    }

    function handleTabKey(event, editorElement) {
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();

        focusEditor(editorElement);

        const range = getSelectionRange(editorElement);
        if (!range) {
            return true;
        }

        const currentListItemElement =
            getClosestAncestorTag(range.startContainer, "li", editorElement) ||
            getClosestAncestorTag(range.endContainer, "li", editorElement);

        // Tab inside a list = indent/outdent
        if (currentListItemElement) {
            const didChangeIndentation = event.shiftKey
                ? outdentCurrentListItem(editorElement, currentListItemElement)
                : indentCurrentListItem(editorElement, currentListItemElement);

            if (didChangeIndentation) {
                notifyEditorChanged(editorElement);
                notifySelectionChanged(editorElement);
            }

            return true;
        }

        // Tab outside a list = insert spaces
        insertTabSpacesAtCursor(editorElement);
        notifyEditorChanged(editorElement);
        notifySelectionChanged(editorElement);

        return true;
    }

    /**
     * Registers event listeners exactly once per editor element.
     *
     * @param {HTMLElement} editorElement
     */
    function registerEditorEvents(editorElement) {
        const state = getEditorState(editorElement);

        if (editorElement.__mailEditorEventsRegistered) {
            return;
        }

        const toolbarElement = editorElement
            .closest(".mail-template-editor-shell")
            ?.querySelector(".mail-template-editor-toolbar");

        if (toolbarElement) {
            toolbarElement.addEventListener("mousedown", event => {
                const buttonElement = event.target.closest("button");
                if (buttonElement) {
                    event.preventDefault();
                }
            });
        }

        editorElement.addEventListener("keydown", event => {
            if (event.key === "Tab") {
                const handledTab = handleTabKey(event, editorElement);

                if (handledTab) {
                    return;
                }
            }

            handleKeyboardShortcuts(event, editorElement);
        });
        
        editorElement.addEventListener("paste", event => {
            handlePaste(event, editorElement);
        });

        editorElement.addEventListener("beforeinput", () => {
            if (state.isApplyingHistory) {
                return;
            }

            saveCurrentSelectionRange(editorElement);
        });

        editorElement.addEventListener("input", () => {
            if (state.isApplyingHistory) {
                return;
            }

            saveCurrentSelectionRange(editorElement);
            queueHistorySnapshot(editorElement);
            notifyEditorChanged(editorElement);
        });

        editorElement.addEventListener("keyup", () => {
            if (state.isApplyingHistory) {
                return;
            }

            saveCurrentSelectionRange(editorElement);
            notifySelectionChanged(editorElement);
        });

        editorElement.addEventListener("mouseup", () => {
            saveCurrentSelectionRange(editorElement);
            notifySelectionChanged(editorElement);
        });

        editorElement.addEventListener("focus", () => {
            if (!restoreSavedSelectionRange(editorElement)) {
                saveCurrentSelectionRange(editorElement);
            }

            notifySelectionChanged(editorElement);
        });

        editorElement.addEventListener("blur", () => {
            if (state.isApplyingHistory) {
                return;
            }

            saveCurrentSelectionRange(editorElement);
        });

        editorElement.addEventListener("dragover", event => {
            event.preventDefault();
            editorElement.classList.add("drag-over");
        });

        editorElement.addEventListener("dragleave", () => {
            editorElement.classList.remove("drag-over");
        });

        editorElement.addEventListener("drop", async event => {
            event.preventDefault();
            editorElement.classList.remove("drag-over");

            const droppedFile = Array.from(event.dataTransfer?.files || [])
                .find(file => file.type.startsWith("image/"));

            if (droppedFile) {
                await tryInsertImageFromFile(editorElement, droppedFile);
            }
        });

        editorElement.__mailEditorEventsRegistered = true;
    }

    return {
        /**
         * Initializes an editor element and primes its history.
         *
         * @param {HTMLElement} editorElement
         * @param {string} [initialHtml]
         * @param dotNetReference
         */
        initialize(editorElement, initialHtml, dotNetReference) {
            if (!editorElement) {
                return;
            }

            const state = getEditorState(editorElement);

            if (dotNetReference) {
                state.dotNetReference = dotNetReference;
            }

            editorElement.innerHTML = initialHtml || "";
            normalizeEditorHtml(editorElement);
            registerEditorEvents(editorElement);
            pushHistorySnapshot(editorElement);
        },

        /**
         * Returns editor HTML.
         *
         * @param {HTMLElement} editorElement
         * @returns {string}
         */
        getHtml(editorElement) {
            if (!editorElement) {
                return "";
            }
            
            return editorElement.innerHTML;
        },

        /**
         * Returns normalized editor HTML.
         *
         * @param {HTMLElement} editorElement
         * @returns {string}
         */
        getNormalizedHtml(editorElement) {
            if (!editorElement) {
                return "";
            }

            normalizeEditorHtml(editorElement);
            return editorElement.innerHTML;
        },

        /**
         * Replaces the editor content.
         *
         * @param {HTMLElement} editorElement
         * @param {string} html
         */
        setHtml(editorElement, html) {
            if (!editorElement) {
                return;
            }

            editorElement.innerHTML = html || "";
            normalizeEditorHtml(editorElement);
            pushHistorySnapshot(editorElement);
            syncEditor(editorElement);
        },

        toggleInlineTag(editorElement, tagName) {
            toggleInlineTag(editorElement, tagName);
            syncEditor(editorElement);
        },

        wrapSelection(editorElement, tagName) {
            toggleInlineTag(editorElement, tagName);
            syncEditor(editorElement);
        },

        applyBlockTag(editorElement, tagName) {
            applyBlockTag(editorElement, tagName);
            syncEditor(editorElement);
        },

        toggleUnorderedList(editorElement) {
            toggleList(editorElement, "ul");
            syncEditor(editorElement);
        },

        toggleOrderedList(editorElement) {
            toggleList(editorElement, "ol");
            syncEditor(editorElement);
        },

        insertHtmlAtCursor(editorElement, html) {
            insertHtmlAtCursor(editorElement, sanitizeHtml(html));
            syncEditor(editorElement);
        },

        insertOrUpdateLink(editorElement, url, linkText) {
            insertOrUpdateLink(editorElement, sanitizeUrl(url), linkText);
            syncEditor(editorElement);
        },

        removeLink(editorElement) {
            removeLink(editorElement);
            syncEditor(editorElement);
        },

        clearFormatting(editorElement) {
            clearFormatting(editorElement);
            syncEditor(editorElement);
        },

        applyTextColor(editorElement, colorValue) {
            applyTextColor(editorElement, colorValue);
            syncEditor(editorElement);
        },

        normalizeHtml(editorElement) {
            normalizeEditorHtml(editorElement);
            syncEditor(editorElement);
        },

        /**
         * @param {HTMLElement} editorElement
         * @returns {{ bold: boolean, italic: boolean, underline: boolean, unorderedList: boolean, orderedList: boolean, link: boolean }}
         */
        getActiveFormats(editorElement) {
            return getActiveFormats(editorElement);
        },

        /**
         * @param {HTMLElement} editorElement
         */
        undo(editorElement) {
            undo(editorElement);
        },

        /**
         * @param {HTMLElement} editorElement
         */
        redo(editorElement) {
            redo(editorElement);
        },
        
        /**
         * @param {string} html
         * @returns {string}
         */
        sanitizeHtml(html) {
            return sanitizeHtml(html);
        }
    };
})();

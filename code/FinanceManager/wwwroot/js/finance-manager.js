window.financeManager = window.financeManager || {};

window.financeManager.downloadFileFromBase64 = function (fileName, contentType, base64Content) {
    const byteCharacters = atob(base64Content);
    const byteNumbers = new Array(byteCharacters.length);

    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }

    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(url), 0);
};

window.financeManager.downloadTextFile = function (fileName, contentType, textContent) {
    const blob = new Blob([textContent], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(url), 0);
};

// Fades the bottom of a ScrollFadeArea while its trailing sentinel is scrolled out of view.
window.financeManager.observeScrollFade = function (element) {
    const sentinel = element?.querySelector(':scope > .fm-scroll-fade-sentinel');
    if (!sentinel || !('IntersectionObserver' in window)) {
        return;
    }

    const observer = new IntersectionObserver(entries => {
        const entry = entries[entries.length - 1];
        element.classList.toggle('fm-scroll-fade--active', !entry.isIntersecting);
    }, { root: element });

    observer.observe(sentinel);
    element._fmScrollFadeObserver = observer;
};

window.financeManager.unobserveScrollFade = function (element) {
    element?._fmScrollFadeObserver?.disconnect();
    if (element) {
        delete element._fmScrollFadeObserver;
    }
};

function updateUrlWithoutReloading(url) {
    window.history.pushState({ path: url }, '', url);
}

function forceUpdateNumericInputs() {
    document.querySelectorAll('.rz-numeric-input input').forEach(input => {
        input.blur();
        input.focus();
    });
}

function clickElement(element) {
    element.click();
}
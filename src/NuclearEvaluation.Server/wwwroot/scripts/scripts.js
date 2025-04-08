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

function showError(message) {
    alert("Error: " + message);
}

async function checkAndDownloadFile(url) {
    try {
        const response = await fetch(url);
        if (!response.ok) {
            const errorText = await response.text();
            showError(errorText);
        } else {
            const blob = await response.blob();
            let fileName = "downloadedFile";
            const disposition = response.headers.get('content-disposition');
            if (disposition && disposition.indexOf('filename=') !== -1) {
                const filenameRegex = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/;
                const matches = filenameRegex.exec(disposition);
                if (matches != null && matches[1]) {
                    fileName = matches[1].replace(/['"]/g, '');
                }
            }
            const downloadUrl = window.URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = downloadUrl;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(downloadUrl);
        }
    } catch (error) {
        showError(error.message);
    }
}

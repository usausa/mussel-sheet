window.exampleSave = (data, fileName) => {
    const link = document.createElement('a');
    link.href = URL.createObjectURL(data instanceof Blob ? data : new Blob([data]));
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(link.href);
};

// Returns an error message, or null
window.exampleDownload = async (url, json, fileName) => {
    const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: json });
    if (!response.ok) {
        return `${response.status} ${response.statusText}`;
    }

    window.exampleSave(await response.blob(), fileName);
    return null;
};

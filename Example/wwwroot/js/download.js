// Posts JSON and saves the response as a file. Returns an error message, or null
window.exampleDownload = async (url, json, fileName) => {
    const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: json });
    if (!response.ok) {
        return `${response.status} ${response.statusText}`;
    }

    const link = document.createElement('a');
    link.href = URL.createObjectURL(await response.blob());
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(link.href);
    return null;
};

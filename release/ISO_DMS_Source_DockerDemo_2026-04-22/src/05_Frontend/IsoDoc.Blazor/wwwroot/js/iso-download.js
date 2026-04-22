window.isoDownloadFile = function (filename, contentType, base64) {
    const link = document.createElement('a');
    link.download = filename;
    link.href = 'data:' + contentType + ';base64,' + base64;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.isoDocFileTransfer = {
    fetchAuthorizedAsBlobUrl: async function (fullUrl, bearerToken) {
        const headers = {};
        if (bearerToken) {
            headers['Authorization'] = 'Bearer ' + bearerToken;
        }
        const response = await fetch(fullUrl, { headers: headers });
        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || response.statusText);
        }
        const blob = await response.blob();
        return URL.createObjectURL(blob);
    },
    revokeBlobUrl: function (url) {
        if (url && String(url).startsWith('blob:')) {
            URL.revokeObjectURL(url);
        }
    },
    openInNewTab: function (url) {
        window.open(url, '_blank', 'noopener,noreferrer');
    },
    downloadBlobUrl: function (blobUrl, fileName) {
        const a = document.createElement('a');
        a.href = blobUrl;
        a.download = fileName || 'download';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }
};

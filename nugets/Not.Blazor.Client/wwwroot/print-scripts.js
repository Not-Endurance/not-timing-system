export function downloadBytes(fileName, bytes, contentType) {
    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");

    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    setTimeout(() => URL.revokeObjectURL(url), 0);
}

export function printPdfBytes(bytes) {
    const blob = new Blob([bytes], { type: "application/pdf" });
    const url = URL.createObjectURL(blob);
    const iframe = document.createElement("iframe");

    iframe.style.position = "fixed";
    iframe.style.right = "0";
    iframe.style.bottom = "0";
    iframe.style.width = "0";
    iframe.style.height = "0";
    iframe.style.border = "0";
    iframe.src = url;

    iframe.onload = function () {
        setTimeout(() => {
            iframe.contentWindow.focus();
            iframe.contentWindow.print();
        }, 250);
    };

    document.body.appendChild(iframe);
    setTimeout(() => {
        URL.revokeObjectURL(url);
        iframe.remove();
    }, 60000);
}

export function printHtml(html) {
    const iframe = document.createElement("iframe");

    iframe.style.position = "fixed";
    iframe.style.right = "0";
    iframe.style.bottom = "0";
    iframe.style.width = "0";
    iframe.style.height = "0";
    iframe.style.border = "0";

    document.body.appendChild(iframe);

    const printDocument = iframe.contentDocument || iframe.contentWindow.document;
    printDocument.open();
    printDocument.write(html);
    printDocument.close();

    setTimeout(() => {
        iframe.contentWindow.focus();
        iframe.contentWindow.print();
    }, 250);

    setTimeout(() => {
        iframe.remove();
    }, 60000);
}

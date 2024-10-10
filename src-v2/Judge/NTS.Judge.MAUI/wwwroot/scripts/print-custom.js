export function printCustom() {
    insertPageBreaks();
    var padding = removeMainContentPadding()
    window.print();
    restoreMainContentPadding(padding);
}

function removeMainContentPadding() {
    var main = document.querySelector('.mud-main-content');
    let padding = main.style.paddingTop;
    main.style.paddingTop = 0;
    return padding;
}

function restoreMainContentPadding(padding) {
    var main = document.querySelector('.mud-main-content');
    main.style.paddingTop = padding;
}

function getDpi() {
    // Create an element
    let div = document.createElement("div");

    // Set the size in inches (1 inch in CSS)
    div.style.width = "1in";
    div.style.height = "1in";

    // Add the element to the document
    document.body.appendChild(div);

    // Measure the size in pixels
    let dpi = div.offsetWidth;

    // Remove the element
    document.body.removeChild(div);

    // Return DPI value
    return dpi;
}

function getA4SizeInPixels() {
    let dpi = getDpi();
    let a4Width = 210 * (dpi / 25.4);  // A4 width in mm, converted to inches
    let a4Height = 297 * (dpi / 25.4); // A4 height in mm, converted to inches

    return { width: a4Width, height: a4Height };
}

function insertPageBreaks() {
    let a4Height = getA4SizeInPixels().height;
    let remainingHeight = a4Height;
    let printablesCount = 0;
    console.log('A4 height', remainingHeight)

    var printables = document.querySelectorAll('.printable');
    console.log('printables', printables)

    printables.forEach(printable => {
        console.log('article', ++printablesCount)
        console.log('printable', printable)

        let articleHeight = printable.offsetHeight;
        console.log('article height', articleHeight)

        if (articleHeight > remainingHeight) {
            printable.style.pageBreakBefore = 'always';
            remainingHeight = a4Height;
            console.log('inserting page break')
        }

        remainingHeight -= articleHeight;
        console.log('remaining height', remainingHeight)
    });
}
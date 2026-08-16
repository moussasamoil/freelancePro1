
// Function to show the dynamic notification
function showNotification(message, confirmAction, imageUrl, iconBg, iconWidth) {

    console.log("showNotification called", { message, imageUrl, iconBg, iconWidth, hasConfirmAction: !!confirmAction });

    var overlay = $('#pageOverlay');
    var notification = $('#dynamicNotification');
    var confirmBtn = $('#confirmAction');
    console.log("DOM elements found — overlay:", overlay.length, "notification:", notification.length, "confirmBtn:", confirmBtn.length);

    // Set the message and image dynamically
    $('#notificationText').text(message);
    $('#notificationIcon').attr('src', imageUrl).attr('width', iconWidth || 30);
    $('#notificationIconWrapper').css('background-color', iconBg || '');

    // Attach confirmAction function to the confirm button
    confirmBtn.off('click').on('click', function () {
        if (confirmAction) confirmAction();
        hideNotification();
    });

    // Show the overlay and dim inputs
    overlay.show();
    $('input, textarea, select').css('background-color', 'rgba(33, 37, 41, 0.1)');
    notification.show();
    document.body.style.backgroundColor = "rgba(33, 37, 41, 0.1)";

    var overlayDisplay = overlay.css('display');
    var notifDisplay = notification.css('display');
    var overlayZ = overlay.css('z-index');
    var notifZ = notification.css('z-index');
    var overlayRect = overlay.length ? overlay[0].getBoundingClientRect() : null;
    var notifRect = notification.length ? notification[0].getBoundingClientRect() : null;
    console.log("After show — overlay:", { display: overlayDisplay, zIndex: overlayZ, rect: overlayRect });
    console.log("After show — notification:", { display: notifDisplay, zIndex: notifZ, rect: notifRect });

    // Check if modal is open and could be blocking
    var modal = document.querySelector('.modal.show');
    if (modal) {
        var modalZ = window.getComputedStyle(modal).zIndex;
        console.log("Active modal detected — zIndex:", modalZ, "id:", modal.id);
    }
}

function showAutoNotification(message, imageUrl, iconBg, iconWidth) {
    $('#notificationText').text(message);
    $('#notificationIcon').attr('src', imageUrl).attr('width', iconWidth || 30);
    $('#notificationIconWrapper').css('background-color', iconBg || '');

    $('#notificationButtons').hide();
    $('#notificationAutoBar').show();

    // Reset and restart the drain animation
    var $bar = $('#notificationLoadBar');
    $bar.css('animation', 'none');
    $bar[0].offsetWidth; // force reflow
    $bar.css('animation', 'notificationDrain 2s linear forwards');

    $('#pageOverlay').show();
    $('input, textarea, select').css('background-color', 'rgba(33, 37, 41, 0.1)');
    $('#dynamicNotification').show();
    document.body.style.backgroundColor = "rgba(33, 37, 41, 0.1)";

    setTimeout(function () {
        hideNotification();
        $('#notificationButtons').show();
        $('#notificationAutoBar').hide();
    }, 2000);
}

// Function to hide the dynamic notification
function hideNotification() {
    $('#dynamicNotification').hide();  // Hide notification
    $('#pageOverlay').hide();  // Hide overlay
    document.body.style.backgroundColor = "";  // Reset background
    $('input, textarea, select').css('background-color', '');  // Reset background color for inputs
}

// Use event delegation to handle dynamically added elements
$(document).on('click', '#closeNotification', function () {
    hideNotification();  // Hide notification when close is clicked
});






// Define a key to store the current page URL in sessionStorage
const sessionKey = "currentPage";

// Get the current full URL (this includes query parameters to track filters as well)
const currentPage = window.location.href;

// Get the previous page URL from sessionStorage
const previousPage = sessionStorage.getItem(sessionKey);

// Check if the page was refreshed (using performance navigation type)
const isPageRefreshed = performance.navigation.type === performance.navigation.TYPE_RELOAD;

// Check if query parameters are present (i.e., filters are applied)
const hasQueryParams = !!window.location.search;

// Log current situation
console.log("Current Page URL:", currentPage);
console.log("Previous Page URL:", previousPage);
console.log("Is Page Refreshed:", isPageRefreshed);
console.log("Query Params:", window.location.search);

// Function to clear filters
function callClearFilter() {
    console.log("Calling /Home/ClearFilter to reset filters...");
    $.ajax({
        url: '/Home/ClearFilters',
        type: 'POST',
        success: function (response) {
            console.log('Filters cleared successfully.');
        },
        error: function (error) {
            console.error('Error clearing filters:', error);
        }
    });
}


// Logic to handle page refresh and navigation
if (previousPage !== currentPage) {
    // If the user navigates to a new page or comes back from another page, set the session storage to the new page
    console.log("Navigated to a new page.");
    sessionStorage.setItem(sessionKey, currentPage);
} else if (isPageRefreshed) {
    // If the page was refreshed
    console.log("Page was refreshed.");

    if (!hasQueryParams) {
        // If no query parameters are present, treat it as a page refresh and clear filters
        console.log("No query filters applied, calling ClearFilter.");
        callClearFilter();
    } else {
        // If query filters are present, do not clear filters
        console.log("Query filters are present, not calling ClearFilter.");
    }
} else {
    // If user is navigating between pages, update the session key
    console.log("Navigating between pages, storing the current page.");
    sessionStorage.setItem(sessionKey, currentPage);
}


function displayNotification(orderJson) {
    const orderObject = JSON.parse(orderJson);
    const order = orderObject.Order;

    const notification = new Notification("تم اضافة طلب رقم " + order.Id, {
        icon: "/static/LuxiraRounded.png", // Ensure you have an icon at this path
        requireInteraction: true  // Make notification persistent
    });
    notification.dir = "rtl"; // Set the direction to right-to-left

    notification.onclick = function () {
        window.focus(); // Focus the tab if it's open
        window.location.href = '/order/details/' + order.Id; // Navigate to order details page
        this.close(); // Close notification after clicking
    };
}

// Function to handle success status notification

function handleNotificationsuccess(orderId, orderCountry) {

    const notification = new Notification("تم تسليم طلب رقم " + orderId, {
        icon: "/static/LuxiraRounded.png", // Ensure you have an icon at this path
        requireInteraction: true,// Make notification persistent
        body: "البلد: " + orderCountry, // Set the notification body with the order country

    });
    notification.dir = "rtl"; // Set the direction to right-to-left

    notification.onclick = function () {
        window.focus(); // Focus the tab if it's open
        window.location.href = '/order/details/' + orderId; // Navigate to order details page
        this.close(); // Close notification after clicking
    };
}

// Function to handle failed status notification
function handleNotificationfailed(orderId, orderCountry) {
    // Create a new notification with order details
    const notification = new Notification("جاري ارجاع طلب رقم " + orderId, {
        icon: "/static/luxiraroundednotext.png",  // Notification icon
        requireInteraction: true,  // Make notification persistent until interacted with
        body: "البلد: " + orderCountry,  // Notification body with country information
        dir: "ltr",  // Ensures that the layout is right-to-left, which typically puts the icon to
    });

    // Define what happens when the notification is clicked
    notification.onclick = function () {
        window.focus();  // Focus the tab if it's open
        window.location.href = '/order/details/' + orderId;  // Navigate to order details page
        this.close();  // Close the notification after clicking
    };
}


function handleNotificationfixed(orderId, orderCountry) {

    const notification = new Notification("تم معالجة طلب رقم " + orderId, {
        icon: "/static/LuxiraRounded.png", // Ensure you have an icon at this path
        requireInteraction: true,// Make notification persistent
        body: "البلد: " + orderCountry, // Set the notification body with the order country
    });
    notification.dir = "rtl"; // Set the direction to right-to-left

    notification.onclick = function () {
        window.focus(); // Focus the tab if it's open
        window.location.href = '/order/details/' + orderId; // Navigate to order details page
        this.close(); // Close notification after clicking
    };
}

// connection starting 
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/orderHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();



connection.start()
    .then(() => console.log("SignalR connected"))
    .catch(err => console.error("SignalR connection error:", err.toString()));



// for order create 
connection.on("NotifyOrderAdded", (order) => {
    console.log("NotifyOrderAdded event triggered");
    console.log(order);
    try {
        if (order) {
            addOrderToTable(order);
            console.log("Order added to table");
            if (!window.isAdmin) {
                var audio = new Audio('/NotificationSound/NewOrder.mp3');
                audio.play();
            }

          

            // Original notification logic
            if (Notification.permission === "granted") {
                displayNotification(order);
                console.log("Order notification displayed");
            } else if (Notification.permission !== "denied") {
                Notification.requestPermission().then(permission => {
                    if (permission === "granted") {
                        displayNotification(order);
                        console.log("Order notification displayed after permission granted");
                    }
                });
            }
        } else {
            console.error("Invalid order format:", order);
        }
    } catch (error) {
        console.error("Error processing order:", error);
    }
});


// for potential order create
connection.on("NotifyPotentialOrderAdded", (potentialOrder) => {
    console.log("NotifyPotentialOrderAdded event triggered");
    console.log(potentialOrder);
    try {
        if (potentialOrder) {
            var audio = new Audio('/NotificationSound/PotentialOrder.mp3');
            audio.play();

            if (Notification.permission === "granted") {
                displayPotentialOrderNotification(potentialOrder);
            } else if (Notification.permission !== "denied") {
                Notification.requestPermission().then(permission => {
                    if (permission === "granted") {
                        displayPotentialOrderNotification(potentialOrder);
                    }
                });
            }
        }
    } catch (error) {
        console.error("Error processing potential order:", error);
    }
});

function displayPotentialOrderNotification(potentialOrderJson) {
    const potentialOrderObject = JSON.parse(potentialOrderJson);
    const po = potentialOrderObject.PotentialOrder;

    const notification = new Notification("تم اضافة عميل محتمل رقم " + po.Id, {
        icon: "/static/LuxiraRounded.png",
        requireInteraction: true
    });
    notification.dir = "rtl";

    notification.onclick = function () {
        window.focus();
        window.location.href = '/PotentialOrder';
        this.close();
    };
}

connection.on("successorderstatusnotification", function (orderId, orderCountry) {
    console.log(" أشعار تم  التسليم ");

    // Call the function to handle the notification
    handleNotificationsuccess(orderId, orderCountry);

});

connection.on("failedorderstatusnotification", function (orderId, orderCountry) {
    console.log(" أشعار فشل   التسليم ");

    // Call the function to handle the notification
    handleNotificationfailed(orderId, orderCountry);
});

connection.on("fixedorderstatusnotification", function (orderId, orderCountry) {
    console.log(" أشعار تم  المعالحة ");

    // Call the function to handle the notification
    handleNotificationfixed(orderId, orderCountry);

});

connection.on("failedorderstatussound", (status) => {
    // Play a sound or show a notification
    console.log("صوت فشل التسليم ");

    var audio = new Audio('/NotificationSound/Alert.mp3');
    audio.play();
});

// update status on order details page 
connection.on("OrderStatusUpdated", (order) => {
    console.log("Order status updated:", order);

    // Access the orderId property correctly
    const receivedOrderId = order.orderId;
    const currentOrderId = parseInt(window.location.pathname.split('/').pop(), 10);

    console.log("Received orderId:", receivedOrderId);
    console.log("Current orderId:", currentOrderId);

    // Check if the order ID from the event matches the current order ID
    if (receivedOrderId === currentOrderId) {
        // Call the updateStatusDisplay function to update the status in the UI
        updateStatusDisplay(order);
    } else {
        console.error("Order ID does not match, no update will be applied.");
    }
});


// details page  مدخل الطلب
connection.on("OrderApplicationUserUpdated", function (data) {
    console.log("Order application user updated:", data);

    // Extract the current orderId from the URL
    const urlParts = window.location.pathname.split('/');
    const currentOrderId = parseInt(urlParts[urlParts.length - 1], 10); // Get the last part of the URL and parse it as an integer

    console.log("Received orderId:", data.orderId);
    console.log("Current orderId:", currentOrderId);

    if (data && data.orderId === currentOrderId) {
        const employeeImageElement = document.getElementById('orderDetails_employeeImage');
        const employeeNameElement = document.getElementById('orderDetails_employeeName');

        if (employeeImageElement) {
            employeeImageElement.src = "/" + data.employeeImage;
        }

        if (employeeNameElement) {
            employeeNameElement.textContent = data.employeeName;
        }
    } else {
        console.error("Invalid data received or orderId does not match:", data);
    }
});


// details page  شركة التوصيل
connection.on("UpdateOrderDeliveryCompany", function (data) {
    console.log("Delivery company update received:", data);

    // Extract the current orderId from the URL
    const urlParts = window.location.pathname.split('/');
    const currentOrderId = parseInt(urlParts[urlParts.length - 1], 10);

    console.log("Received orderId (from data):", data.orderId);
    console.log("Current orderId (from URL):", currentOrderId);
    $('#orderDetails_deliveryCost').text(data.deliveryPrice);
    $('#orderDetails_remainingAmount').text(orderDetails.remainingPrice);

    if (!isNaN(data.orderId) && data.orderId === currentOrderId) {
        const deliveryCompanyLogoElement = document.getElementById('orderDetails_deliveryCompanyLogo');
        const deliveryCompanyNameElement = document.getElementById('orderDetails_deliveryCompanyName');

        if (deliveryCompanyLogoElement) {
            deliveryCompanyLogoElement.src = data.deliveryCompanyLogoUrl || 'static/DefaultImage.svg';
            console.log("Updated delivery company logo URL:", data.deliveryCompanyLogoUrl || 'static/DefaultImage.svg');
        } else {
            console.error("deliveryCompanyLogoElement not found");
        }

        if (deliveryCompanyNameElement) {
            deliveryCompanyNameElement.textContent = data.deliveryCompanyName || 'Unknown';
            console.log("Updated delivery company name:", data.deliveryCompanyName || 'Unknown');
        } else {
            console.error("deliveryCompanyNameElement not found");
        }
    } else {
        console.error("Order ID mismatch or invalid data received:", data);
    }
});

// Update for Client Type (عميل مميز)
connection.on("UpdateOrderClientType", function (data) {
    console.log("Client type update received:", data);

    // Parse the current Order ID from the URL
    const urlParts = window.location.pathname.split('/');
    const currentOrderId = parseInt(urlParts[urlParts.length - 1], 10);

    // Check for both OrderId and orderId to ensure consistent behavior
    const receivedOrderId = data.OrderId || data.orderId;

    if (receivedOrderId === currentOrderId) {
        // Update the special client button background and text color
        const specialClientButton = document.getElementById('toggleSpecialClientButton');
        if (specialClientButton) {
            if (data.isClientSpecial) {
                // Apply the purple background color and text color for a special client
                specialClientButton.setAttribute("style", "background-color:#7E30E1!important; color:white;");
                specialClientButton.classList.add('text-white');
            } else {
                // Reset the background and remove the special text color when not a special client
                specialClientButton.setAttribute("style", "background-color:''; color:'';");
                specialClientButton.classList.remove('text-white');
            }
        }

        // Update the client type container on the details page
        const clientTypeContainer = document.getElementById('clientTypeContainerDetailsPage');
        if (clientTypeContainer) {
            if (data.isClientSpecial) {
                clientTypeContainer.innerHTML = `
                    <div class="d-flex align-items-center mt-2">
                        <dt class="mb-1 col-3">نوع العميل</dt>
                        <div style="padding-left:38px; padding-right:38px; background-color:#7E30E1 !important;" 
                             class="me-5 rounded-pill h5 fw-bold text-center py-1 btn-warning text-white">
                            عميل مميز
                        </div>
                    </div>
                `;
            } else {
                clientTypeContainer.innerHTML = ''; // Remove the section if the client is no longer special
            }
        } else {
            console.error("Client type container not found.");
        }
    } else {
        console.error("Invalid data received or orderId does not match:", data);
    }
});


// Update for Complaints Type (شكاوي)
connection.on("UpdateOrderComplaintsType", function (data) {
    console.log("Complaints type update received:", data);

    // Parse the current Order ID from the URL
    const urlParts = window.location.pathname.split('/');
    const currentOrderId = parseInt(urlParts[urlParts.length - 1], 10);
    console.log("Current Order ID:", currentOrderId);

    // Check for both OrderId and orderId to ensure consistent behavior
    const receivedOrderId = data.OrderId || data.orderId;
    console.log("Received Order ID:", receivedOrderId);

    if (receivedOrderId === currentOrderId) {
        console.log("Order ID matches, updating UI...");

        // Update the complaints button background and text color
        const complaintsButton = document.getElementById('toggleComplaintsButton');
        if (complaintsButton) {
            if (data.IsComplaints || data.isComplaints) {
                console.log("IsComplaints is true, updating button style to red and white");

                // Apply the red background color and white text for a complaint
                complaintsButton.removeAttribute("style"); // Reset styles when not a complaint
                complaintsButton.setAttribute("style", "background-color:red!important; color:white;");
            } else {
                console.log("IsComplaints is false, resetting button style");

                // Reset the background and text color when it's no longer a complaint
                complaintsButton.removeAttribute("style"); // Reset styles when not a complaint
                complaintsButton.classList.remove('text-white');
            }
        } else {
            console.error("Complaints button not found.");
        }

        // Update the complaints container on the details page
        const complaintsContainer = document.getElementById('orderComplaintsContainerDetailsPage');
        if (complaintsContainer) {
            if (data.IsComplaints || data.isComplaints) {
                console.log("Appending complaints type to container.");

                complaintsContainer.innerHTML = `
                    <div class="d-flex align-items-center mt-2">
                        <dt class="mb-1 col-3">نوع الطلب</dt>
                        <div style="padding-left:38px; padding-right:38px; background-color:red !important;" 
                             class="me-5 rounded-pill h5 fw-bold text-center py-1 btn-warning text-white">
                            شكاوي
                        </div>
                    </div>
                `;
            } else {
                console.log("Clearing complaints container as it's no longer a complaint.");
                complaintsContainer.innerHTML = ''; // Remove the section if it's no longer a complaint
            }
        } else {
            console.error("Complaints container not found.");
        }
    } else {
        console.error("Invalid data received or orderId does not match:", data);
    }
});

// Connection setup for hide order in details page 
connection.on("OrderHiddenStatusUpdated", function (data) {
    console.log("Order hidden status update received:", data);

    // Extract the current orderId from the URL
    const urlParts = window.location.pathname.split('/');
    const currentOrderId = parseInt(urlParts[urlParts.length - 1], 10);

    console.log("Received orderId:", data.orderId);
    console.log("Current orderId:", currentOrderId);

    if (data && data.orderId && currentOrderId && data.orderId === currentOrderId) {
        const hiddenStatusDiv = document.getElementById('orderHiddenStatusDivDetailsPage');

        if (hiddenStatusDiv) {
            if (data.isHidden) {
                hiddenStatusDiv.style.display = 'block'; // Show the div
                console.log("Order is now hidden, showing the div.");
            } else {
                hiddenStatusDiv.style.display = 'none'; // Hide the div
                console.log("Order is now unhidden, hiding the div.");
            }
        } else {
            console.error("orderHiddenStatusDivDetailsPage element not found");
        }
    } else {
        console.error("Invalid data received or orderId does not match:", data);
    }
});

connection.on("OrderStatusHistoryDelete", function (data) {
    console.log("Order status history delete update received:", data);

    let historyId = parseInt(data.historyId, 10);
    if (isNaN(historyId)) {
        console.error("Invalid historyId received");
        return;
    }

    const isDeleted = data.isDeleted;
    const isHidden = data.isHidden;

    // Find the row using the history ID
    const selector = `.order-status-history-row[data-history-id='${historyId}']`;
    const statusRowElement = document.querySelector(selector);

    if (statusRowElement) {
        if (isDeleted) {
            // Remove the deleted row
            statusRowElement.remove();
            console.log(`Deleted status row for historyId: ${historyId}`);

            // Find all remaining rows after deletion
            const remainingRows = Array.from(document.querySelectorAll('.order-status-history-row'));

            if (remainingRows.length > 0) {
                // Target the first remaining row after deletion
                const firstRow = remainingRows[0];

                // Find the div with the vertical line class in the first remaining row
                const firstStatusDiv = firstRow.querySelector('.vertical-linelocal');
                if (firstStatusDiv) {
                    // Add 'animateVisibility' and remove 'vertical-linelocal' from the first remaining row
                    firstStatusDiv.classList.add('animateVisibility');
                    firstStatusDiv.classList.remove('vertical-linelocal');
                    console.log('Applied animateVisibility and removed vertical-linelocal for the first remaining row');
                }
            } else {
                console.log('No more rows left after deletion.');
            }
        } else if (isHidden) {
            // Hide the row
            statusRowElement.style.display = 'none';
            console.log(`Hidden status row for historyId: ${historyId}`);
        }
    } else {
        console.error(`Status row element not found for historyId: ${historyId}`);
    }
});


// Connection setup for order edit in details page 
connection.on("OrderDetailsUpdated", function (data) {
    console.log("Raw data received from SignalR:", data);

    var orderDetails;
    if (typeof data === 'string') {
        try {
            orderDetails = JSON.parse(data);
            console.log("Parsed JSON data:", orderDetails);
        } catch (error) {
            console.error("Failed to parse JSON data:", error);
            return;
        }
    } else {
        orderDetails = data;
        console.log("Received data as object:", orderDetails);
    }

    const urlParts = window.location.pathname.split('/');
    const currentOrderId = parseInt(urlParts[urlParts.length - 1], 10);

    console.log("Current Order ID from URL:", currentOrderId);
    console.log("Received Order ID:", orderDetails.id);

    // Update payment status

        // Get the payment status element
        var paymentElement = $('#orderdetails_ispaid');

    if (orderDetails.isPaid) {
            paymentElement.text("تم التحويل حوالة بنكية");
            paymentElement.css({
                "padding": "8px 55px",
                "background-color": "#28a745"
            });
        } else {
            paymentElement.text("غير مدفوع");
            paymentElement.css({
                "padding": "8px 39px",
                "background-color": "red"
            });
        }

        // Update any other elements as needed
    console.log("Payment status updated to:", orderDetails.isPaid);

    // Update other elements as needed

    if (orderDetails && orderDetails.id === currentOrderId) {
        console.log("Order details updated for Order ID:", currentOrderId);

        $('#orderDetails_manufacturingCompanyName').text(orderDetails.manufacturingCompanyName);
        $('#orderDetails_manufacturingCompanyLogo').attr('src', '/' + orderDetails.manufacturingCompanyLogoUrl); // Update manufacturing company logo with leading /
        $('#orderDetails_deliveryCompanyName').text(orderDetails.deliveryCompanyName);

        $('#orderDetails_deliveryCompanyLogo').attr('src', '/' + orderDetails.deliveryCompanyLogoUrl); // Update delivery company logo with leading /
        $('#orderDetails_deliveryCost').text(orderDetails.deliveryPrice);
        $('#orderDetails_remainingAmount').text(orderDetails.remainingPrice);

        $('#orderDetails_sourceName').text(orderDetails.sourceName);
        $('#orderDetails_orderSource').text(orderDetails.orderSource);
        $('#orderDetails_orderSourceIcon').attr('src', orderDetails.orderSourceIconUrl); // Update order source icon with leading /
        $('#orderDetails_totalPrice').text(orderDetails.totalPrice);
        $('#orderDetails_state').text(orderDetails.state);
        $('#orderDetails_address').text(orderDetails.address);
        $('#orderDetails_telephoneNumber').text(orderDetails.telephoneNumber);
        $('#orderDetails_notes').text(orderDetails.notes);
        $('#orderdetails_ispaid').text(orderDetails.isPaid ? "تم التحويل حوالة بنكية" : "غير مدفوع");

        var warehouseTable = $('#orderDetails_warehouseData');
        warehouseTable.empty();

        // Update notes - create the element if it doesn't exist
        if (orderDetails.notes) {
            let notesContainer = $('.d-flex.align-items-center.mt-2').filter(function () {
                return $(this).find('dt').text() === 'الملاحظات';
            });

            if (notesContainer.length === 0) {
                // Create the notes element if it doesn't exist
                notesContainer = $(`
                    <div class="d-flex align-items-center mt-2">
                        <dt class="mb-1 col-3">الملاحظات</dt>
                        <dd id="orderDetails_notes" class="me-5 col-8"></dd>
                    </div>
                `);
                // Insert it in a logical place in your DOM
                $('[id*="orderDetails_"]').last().after(notesContainer);
            }
            $('#orderDetails_notes').text(orderDetails.notes);
        }

        // Update order type (fromComments) if applicable
        if (orderDetails.fromComments) {
            let orderTypeContainer = $('.d-flex.align-items-center.mt-2').filter(function () {
                return $(this).find('dt').text() === 'نوع الطلب';
            });

            if (orderTypeContainer.length === 0) {
                // Create the order type element if it doesn't exist
                orderTypeContainer = $(`
                    <div class="d-flex align-items-center mt-2">
                        <dt class="mb-1 col-3">نوع الطلب</dt>
                        <div id="orderDetails_fromComments" style="padding-left:45px; padding-right:45px;" class="me-5 background-yellow rounded-pill h5 fw-bold text-center py-1 btn-warning text-white">
                            التعليقات
                        </div>
                    </div>
                `);
                // Insert it in a logical place in your DOM
                $('[id*="orderDetails_"]').last().after(orderTypeContainer);
            }
        }

        orderDetails.warehouses.forEach(function (warehouse) {
            console.log("Processing warehouse:", warehouse); // Log each warehouse object

            var row = `<tr>
                <td style="border-radius: 50rem !important; overflow: hidden;">
                    <img loading="lazy" src="${warehouse.warehouseimage}" height="20" width="25" />
                </td>
                <td style="border-radius: 50rem !important; overflow: hidden;">
                    ${warehouse.warehouseName}
                </td>
                <td style="border-radius: 50rem !important; overflow: hidden;">
                    ${warehouse.amount}
                </td>
            </tr>`;
            warehouseTable.append(row);
        });
    } else {
        console.log("Received update for a different order. Current Order ID:", currentOrderId, "Received Order ID:", orderDetails ? orderDetails.id : "undefined");
    }
});



// Event listener for FailedOrdersNotification
connection.on("FailedOrdersNotification", function (notificationData) {
    // Log the received notification data for debugging
    console.log("Received Failed Order Notification:", notificationData);

    // Call the function to append the new failed order to the notification dropdown
    prependNewFailedOrder(notificationData);
});

// Function to append a new failed order to the notification dropdown
function prependNewFailedOrder(notificationData) {
    const { orderId, country, deliveryCompanyName, manufacturerCompanyName, failureReason } = notificationData;

    // Get the current date string (you can customize the format if needed)
    const currentDate = new Date().toLocaleDateString();

    // Log the incoming data
    console.log("Prepending failed order:", orderId, country, deliveryCompanyName, manufacturerCompanyName, failureReason);

    // Create a new order row
    var row = document.createElement("div");
    row.classList.add("row", "bell-card", "pointer");

    // Add a click event listener to navigate to the order's details page when clicked
    row.addEventListener("click", function () {
        window.location.href = "order/details/" + orderId;
    });

    // Create image column
    var imgCol = document.createElement("div");
    imgCol.classList.add("col-2", "d-flex", "justify-content-start", "align-items-center", "pe-0");
    var img = document.createElement("img");
    img.src = "/static/notificationmainlayoutgreen.svg"; // Default icon for new notification
    img.width = 35;
    img.height = 35;
    imgCol.appendChild(img);
    row.appendChild(imgCol);

    // Create content column
    var contentCol = document.createElement("div");
    contentCol.classList.add("col-10", "ps-0");

    // Add text elements for the notification
    var paragraph1 = document.createElement("p");
    paragraph1.classList.add("f-s-14");
    paragraph1.textContent = country;

    var paragraph2 = document.createElement("p");
    paragraph2.classList.add("f-s-14", "text-nowrap");
    paragraph2.textContent = "جاري ارجاع طلب رقم " + orderId;

    var paragraph3 = document.createElement("p");
    paragraph3.classList.add("f-s-14");
    paragraph3.textContent = deliveryCompanyName;

    var paragraph4 = document.createElement("p");
    paragraph4.classList.add("f-s-14");
    paragraph4.textContent = manufacturerCompanyName;

    var paragraph5 = document.createElement("p");
    paragraph5.classList.add("f-s-14");
    paragraph5.textContent = failureReason;

    // Append all paragraphs to the content column
    contentCol.appendChild(paragraph1);
    contentCol.appendChild(paragraph2);
    contentCol.appendChild(paragraph3);
    contentCol.appendChild(paragraph4);
    contentCol.appendChild(paragraph5);

    // Append the content column to the row
    row.appendChild(contentCol);

    // Find the notification dropdown element
    var notificationDropdown = document.getElementById("notificationDropdown");

    // Check if the date group already exists
    var existingDateGroup = Array.from(notificationDropdown.getElementsByClassName('date-group-wrapper'))
        .find(group => group.textContent.includes(currentDate));

    if (existingDateGroup) {
        // If the date group exists, prepend the new row under this date group
        console.log("Date group exists. Prepending to existing date group.");
        var shadowedContainer = existingDateGroup.querySelector('.shadowed-container');

    
            row.classList.add("shadowed-container-border-bottom");
        

        shadowedContainer.prepend(row); // Prepend the new order under the correct date
    } else {
        // If the date group does not exist, create a new one
        console.log("Date group does not exist. Creating new date group.");

        var dateGroupWrapper = document.createElement("div");
        dateGroupWrapper.classList.add("date-group-wrapper", "f-s-14", "mt-2");

        // Create and append the date header
        var dateHeader = document.createElement("p");
        dateHeader.textContent = currentDate;
        dateHeader.classList.add("text-green", "me-3");
        dateGroupWrapper.appendChild(dateHeader);

        // Create a shadowed container for the orders
        var shadowedContainer = document.createElement("div");
        shadowedContainer.classList.add("shadowed-container");

        // Since this is the first notification, add the special class
        row.classList.add("shadowed-container-border-bottom");

        shadowedContainer.prepend(row); // Prepend the new order

        // Append the shadowed container to the date group wrapper
        dateGroupWrapper.appendChild(shadowedContainer);

        // Prepend the new date group to the notification dropdown
        notificationDropdown.prepend(dateGroupWrapper);
    }

    // Make sure the notification dropdown is visible
    if (notificationDropdown.style.display === "none") {
        notificationDropdown.style.display = "block";
    }
}

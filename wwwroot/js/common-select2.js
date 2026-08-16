// common-select2.js
$(document).ready(function () {
    // Prevent scroll from bubbling out of any Select2 dropdown to the page
    $(document).on('wheel', '.select2-results', function (e) {
        var el = this;
        if ((el.scrollTop === 0 && e.originalEvent.deltaY < 0) ||
            (el.scrollTop + el.clientHeight >= el.scrollHeight && e.originalEvent.deltaY > 0)) {
            e.preventDefault();
        }
    });

    initializeSelect2();
    fetchCountries();
    fetchDeliveryRepresentative();
    fetchEmployees();
    fetchMainWarehouses();
    fetchOrderSources();
    populateWorkshift();
    populateGender();
    fetchOrderstatuses();
    $('#OrderStatusId').on('select2:open', function () {
        refreshOrderStatusCounts();
        refreshDropdownCounts('orderStatus', '#OrderStatusId');
    });
    $('#CountryId').on('select2:open', function () {
        refreshDropdownCounts('country', '#CountryId');
    });
    $('#CityId').on('select2:open', function () {
        refreshDropdownCounts('city', '#CityId');
    });
    $('#ManufacturingCompanyId').on('select2:open', function () {
        refreshDropdownCounts('store', '#ManufacturingCompanyId');
    });
    $('#DeliveryCompanyId').on('select2:open', function () {
        refreshDropdownCounts('deliveryCompany', '#DeliveryCompanyId');
    });
    $('#DeliveryRepresentativeId').on('select2:open', function () {
        refreshDropdownCounts('deliveryRepresentative', '#DeliveryRepresentativeId');
    });
    $('#OrderSourceId').on('select2:open', function () {
        refreshDropdownCounts('orderSource', '#OrderSourceId');
    });
    $('#FailureReasonId').on('select2:open', function () {
        refreshFailureReasonCounts();
    });
    populateFixedOrders();
    fetchEmployeesIntId();
    populatePotentialOrderStatuses();
    populatePotentialOrderStores();
    fetchCampaignAds();

    // Add event listener for country change
    $('#CountryId').on('change', function () {
        $('#DeliveryCompanyId').val(''); // Reset delivery company dropdown
        $('#DeliveryRepresentativeId').val(''); // Reset delivery company dropdown
        $('#CityId').val(''); // Reset delivery company dropdown
        fetchDeliveryCompanies();
        fetchCitiesByCountry();
        fetchDeliveryRepresentative();
        fetchCampaignAds();

    });
    $('#CityId').on('change', function () {
        $('#DeliveryRepresentativeId').val(''); // Reset delivery company dropdown

        fetchDeliveryRepresentative();

    });

    // Fetch delivery companies and manufacturing companies on page load
    fetchDeliveryCompanies();
    fetchManufacturingCompany();
});

function initializeSelect2() {

    const useDarkHomeIcons =
        window.location.pathname.toLowerCase().startsWith('/home/index');

    const iconPath = useDarkHomeIcons
        ? '/mainicons-dark-full/'
        : '/mainicons/';

    const select2Configs = [
        {
            selector: '#CountryId',
            text: 'تصفية حسب الدولة',
            image: iconPath + 'earth-americas-regular.svg'
        },
        {
            selector: '#DeliveryCompanyId',
            text: 'تصفية حسب شركة التوصيل',
            image: iconPath + 'truck-regular.svg'
        },
        {
            selector: '#ManufacturingCompanyId',
            text: 'تصفية حسب المتجر',
            image: iconPath + 'building-regular.svg'
        },
        {
            selector: '#CityId',
            text: 'تصفية حسب المدينة',
            image: iconPath + 'city-regular.svg'
        },
        {
            selector: '#DeliveryRepresentativeId',
            text: 'تصفية حسب مندوب التوصيل',
            image: iconPath + 'user-tie-regular.svg'
        },
        {
            selector: '#EmployeeId',
            text: 'تصفية حسب الموظف',
            image: iconPath + 'user-regular.svg'
        },
        {
            selector: '#EmployeeIntId',
            text: 'تصفية حسب الموظف',
            image: iconPath + 'user-regular.svg'
        },
        {
            selector: '#ProductId',
            text: 'فلترة حسب المنتج',
            image: iconPath + 'boxes-stacked-regular.svg'
        },
        {
            selector: '#OrderSourceId',
            text: 'فلترة حسب الصفحة',
            image: iconPath + 'hashtag-regular.svg'
        },
        {
            selector: '#GenderId',
            text: 'فلترة حسب الجنس',
            image: iconPath + 'venus-mars-regular.svg'
        },
        {
            selector: '#WorkShift',
            text: 'فلترة حسب الشفت',
            image: iconPath + 'hour.svg'
        },
        {
            selector: '#OrderStatusId',
            text: 'فلترة حسب الحالة',
            image: iconPath + 'filter-regular.svg'
        },
        {
            selector: '#StatusId',
            text: 'فلترة حسب الحالة',
            image: iconPath + 'filter-regular.svg'
        },
        {
            selector: '#StoreNameId',
            text: 'تصفية حسب المتجر',
            image: iconPath + 'building-regular.svg'
        },
        {
            selector: '#statusChangeReason',
            text: 'اختر سبب الفشل',
            image: iconPath + 'circle-exclamation-regular.svg'
        },
        {
            selector: '#FailureReasonId',
            text: 'تصفية حسب سبب الفشل',
            image: iconPath + 'circle-exclamation-regular.svg'
        }
    ];


    select2Configs.forEach(config => {
        $(config.selector).select2({
            placeholder: {
                id: '-1', // Set an ID for the placeholder
                text: config.text,
                image: config.image // Add the image URL to the placeholder
            },
            allowClear: true,
            templateResult: formatOption,
            templateSelection: formatOption,
            dir: "rtl", // Applies RTL layout to both dropdowns.
            minimumResultsForSearch: 1, // Always show the search box.
            width: '100%' // Set width explicitly to 100%
        }).on('select2:open', function () {
            // Hide the first option when the dropdown is opened
            setTimeout(function () {
                $('.select2-results__option:first-child').hide();
            }, 1);
        }).on('select2:select', function () {
            applySelectedStyles(config.selector);
        }).on('select2:unselecting', function (e) {
            // Log the event to see if this event is triggered
            console.log("Unselecting option:", e.params);
            // Log the state of the styles before clearing
            console.log("Before resetting styles:");
            logCurrentStyles(config.selector);

            // Reset styles when the option is unselected
            resetSelectedStyles(config.selector);

            // Log the state of the styles after clearing
            console.log("After resetting styles:");
            logCurrentStyles(config.selector);
        }).on('select2:clear', function () {
            // If select2:clear does trigger, this will log it
            console.log("Clear event triggered");

            // Log the state of the styles before clearing
            console.log("Before clearing styles:");
            logCurrentStyles(config.selector);

            // Reset styles when the selection is cleared
            resetSelectedStyles(config.selector);

            // Log the state of the styles after clearing
            console.log("After clearing styles:");
            logCurrentStyles(config.selector);
        });
    });
}


const useDarkHomeIcons =
    window.location.pathname.toLowerCase().startsWith('/home/index');



function formatOption(option) {
    var imageUrl = $(option.element).data('image');
    var imageTag = imageUrl ? '<img src="' + imageUrl + '" style="width: 14px; height: 14px;" />' : '';

    // Check if the option is selected and add the checkmark
    var checkmark = $(option.element).is(':selected') ?
        '<i class="fa fa-check text-success" style="margin-left: 10px;"></i>' : '';

    // Handle the placeholder specifically
    if (option.id === '-1' || option.id === '') {
        return $(
            '<div class="d-flex justify-content-start align-items-center custom-width">' +
            '<img src="' + option.image + '" style="width: 25px; height: 25px;" />' +
            '<span class="flex-grow-1 pe-1">' + option.text + '</span>' +
            '<i class="fa-duotone fa-chevron-down" style="margin-left: 10px; font-size: 14px !important;"></i>' +
            '</div>'
        );
    }

    // Normal options handling
    var countAttr = $(option.element).data('count');
    var countSpan = '<span class="status-count text-muted" style="font-size:0.85em; margin-right:4px;">' + (countAttr != null ? '(' + countAttr + ')' : '') + '</span>';

    return $(
        '<div class="d-flex justify-content-start align-items-center custom-width">' +
        imageTag +
        '<span class="flex-grow-1 pe-1 fw-bold">' + option.text + countSpan + '</span>' +
        checkmark +
        '</div>'
    );
}


function resetSelectedStyles(selector) {
    const select2Container = $(selector).next('.select2-container');
    const selectionElement = select2Container.find('.select2-selection--single');

    // Only remove the custom styles added by applySelectedStyles, preserve width
    selectionElement.css({
        'background-color': '',
        'box-shadow': ''
    });
    select2Container.css({
        'background-color': '',
        'box-shadow': ''
    });
    // Restore width that Select2 sets during init
    select2Container.css('width', '100%');

    // Show the chevron icon again
    selectionElement.find('.fa-chevron-down').show();

    selectionElement.find('.select2-selection__rendered').css({
        'display': '',
        'flex-direction': ''
    });
}
function logCurrentStyles(selector) {
    const select2Container = $(selector).next('.select2-container');
    const selectionElement = select2Container.find('.select2-selection--single');

    console.log("Container styles:", select2Container.attr('style'));
    console.log("Selection styles:", selectionElement.attr('style'));
}

function formatSelection(option) {
    if (option.id === '-1') {  // Displaying the placeholder in the selection box
        return 'تصفية حسب الدولة';
    }
    return option.text; // or modify as needed to include images, etc.
}


function applySelectedStyles(selector) {
    const select2Container = $(selector).next('.select2-container');
    const selectionElement = select2Container.find('.select2-selection--single');

    // Apply custom styles to the selected element
    selectionElement.css({
        'background-color': 'rgb(229, 255, 250)',
        'box-shadow': 'rgb(223, 244, 239) 0px 0px 4px'
    });

    selectionElement.find('.select2-selection__rendered').css({
        'display': 'flex',
        'flex-direction': 'row-reverse'
    });

    select2Container.each(function () {
        this.style.setProperty('background-color', 'rgb(229, 255, 250)', 'important');
        this.style.setProperty('box-shadow', 'rgb(223, 244, 239) 0px 0px 4px');
    });

    // Find the chevron icon inside the selected element and hide it
    const chevronIcon = selectionElement.find('.fa-chevron-down');
    if (chevronIcon.length > 0) {
        chevronIcon.hide();  // Hide the chevron icon if it exists
    }

    // Hide the checkmark inside the selected element if it exists
    const checkmarkIcon = selectionElement.find('.fa-check');
    if (checkmarkIcon.length > 0) {
        checkmarkIcon.hide();  // Hide the checkmark icon if it exists
    }
}


function fetchCountries() {
    $.ajax({
        url: (typeof pfdCountriesUrl !== 'undefined') ? pfdCountriesUrl : '/DataList/GetAllCountries',
        type: 'GET',
        dataType: 'json',
        success: function (data) {
            var selectElement = $('#CountryId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">  </option>'); // Add default option
            $.each(data, function (index, country) {
                selectElement.append('<option value="' + country.id + '" data-image="' + country.imageUrl + '">' + country.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching countries: ' + error);
        }
    });
}

function fetchCampaignAds() {
    var countryId = $('#CountryId').val(); // Get the selected country IDs as an array from the #Country dropdown

    var ajaxOptions = {
        url: '/DataList/GetCampaignsByCountry',
        type: 'GET',
        delay: 250, // Wait for 250ms before triggering the request
        traditional: true, // Ensure proper serialization of arrays
        dataType: 'json',
        data: {
            countryId: countryId // Pass the array of country IDs as a parameter
        },
        success: function (data) {
            var selectElement = $('#CampaignId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">     </option>'); // Add option to reset filter
            $.each(data, function (index, company) {
                selectElement.append('<option value="' + company.id + '" data-image="' + company.imageUrl + '">' + company.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching campagins ads: ' + error);
        }
    };

    $.ajax(ajaxOptions);
}


function fetchDeliveryCompanies() {
    var countryIds = $('#CountryId').val(); // Get the selected country IDs as an array from the #Country dropdown

    var ajaxOptions = {
        url: '/DataList/GetAllDeliveryCompanies',
        type: 'GET',
        delay: 250, // Wait for 250ms before triggering the request
        traditional: true, // Ensure proper serialization of arrays
        dataType: 'json',
        data: {
            countryIds: countryIds // Pass the array of country IDs as a parameter
        },
        success: function (data) {
            var selectElement = $('#DeliveryCompanyId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">     </option>'); // Add option to reset filter
            $.each(data, function (index, company) {
                selectElement.append('<option value="' + company.id + '" data-image="/' + company.logoUrl + '">' + company.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching delivery companies: ' + error);
        }
    };

    $.ajax(ajaxOptions);
}



function fetchManufacturingCompany() {
    $.ajax({
        url: '/DataList/GetAllStores',
        type: 'GET',
        delay: 250, // Wait for 250ms before triggering the request

        dataType: 'json',
        success: function (data) {
            var selectElement = $('#ManufacturingCompanyId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">    </option>'); // Add option to reset filter
            $.each(data, function (index, store) {
                selectElement.append('<option value="' + store.id + '" data-image="/' + store.logoUrl + '">' + store.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching manufacturing companies: ' + error);
        }
    });
}


function fetchCitiesByCountry() {
    var countryIds = $('#CountryId').val(); // Get the selected country IDs as an array from the #Country dropdown

    var ajaxOptions = {
        url: '/DataList/GetCitiesByCountry',
        type: 'GET',
        delay: 250, // Wait for 250ms before triggering the request
        traditional: true, // Ensure proper serialization of arrays
        dataType: 'json',
        data: {
            countryIds: countryIds // Pass the array of country IDs as a parameter
        },
        success: function (data) {
            var selectElement = $('#CityId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">    </option>'); // Add option to reset filter
            $.each(data, function (index, city) {
                selectElement.append('<option value="' + city + '">' + city + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching cities:', error);
        }
    };

    $.ajax(ajaxOptions);
}


function fetchDeliveryRepresentative() {
    var countryIds = $('#CountryId').val(); // Get the selected country IDs as an array
    var cityIds = $('#CityId').val(); // Get the selected city IDs as an array

    var ajaxOptions = {
        url: '/DataList/GetAllDeliveryRepresentatives',
        type: 'GET',
        traditional: true, // Ensure proper serialization of arrays
        dataType: 'json',
        data: {
            countryIds: countryIds, // Pass the country IDs as an array
            cityIds: cityIds // Pass the city IDs as an array
        },
        success: function (data) {
            var selectElement = $('#DeliveryRepresentativeId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">    </option>'); // Add option to reset filter
            $.each(data, function (index, representative) {
                selectElement.append('<option value="' + representative.id + '" data-image="/' + representative.logoUrl + '">' + representative.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching DeliveryRepresentativeId:', error);
        }
    };

    $.ajax(ajaxOptions);
}



// Function to fetchEmployees and populate the dropdown
function fetchEmployees() {
    $.ajax({
        url: '/DataList/GetAllEmployees',
        type: 'GET',
        delay: 250, // Wait for 250ms before triggering the request

        dataType: 'json',
        success: function (data) {
            var selectElement = $('#EmployeeId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">  </option>'); // Add option to reset filter
            $.each(data, function (index, employee) {
                selectElement.append('<option value="' + employee.id + '" data-image="/' + employee.logoUrl + '">' + employee.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching delivery companies: ' + error);
        }
    });
}
function fetchEmployeesIntId() {
    $.ajax({
        url: '/DataList/GetAllEmployeesintId',
        type: 'GET',
        delay: 250, // Wait for 250ms before triggering the request
        dataType: 'json',
        success: function (data) {
            var selectElement = $('#EmployeeIntId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">  </option>'); // Add option to reset filter
            $.each(data, function (index, employee) {
                selectElement.append('<option value="' + employee.id + '" data-image="/' + employee.logoUrl + '">' + employee.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching delivery companies: ' + error);
        }
    });
}


function fetchMainWarehouses() {
    $.ajax({
        url: '/DataList/GetMainWarehouses',
        type: 'GET',
        delay: 250, // Wait for 250ms before triggering the request

        success: function (data) {
            var selectElement = $('#ProductId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value="">  </option>'); // Add option to reset filter
            $.each(data, function (index, product) {
                selectElement.append('<option value="' + product.id + '" data-image="/' + product.logoUrl + '">' + product.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching product : ' + error);
        }
    });
}


function fetchOrderSources() {
    $.ajax({
        url: '/DataList/GetAllOrderSources',
        type: 'GET',
        dataType: 'json',
        delay: 250, // Wait for 250ms before triggering the request

        success: function (data) {
            var selectElement = $('#OrderSourceId');
            selectElement.empty(); // Clear previous options
            selectElement.append('<option value=""> </option>'); // Add default option
            $.each(data, function (index, source) {
                selectElement.append('<option value="' + source.id + '" data-image="' + source.imageUrl + '">' + source.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching statuses: ' + error);
        }
    });
}



var _orderStatusesCache = null;

function fetchOrderstatuses() {
    $.ajax({
        url: '/DataList/GetAllOrderStatuses',
        type: 'GET',
        dataType: 'json',
        success: function (data) {
            _orderStatusesCache = data;
            var selectElement = $('#OrderStatusId');
            var currentVal = selectElement.val();
            selectElement.empty();
            selectElement.append('<option value="">  </option>');
            $.each(data, function (index, status) {
                selectElement.append('<option value="' + status.id + '" data-image="' + status.imageUrl + '">' + status.name + '</option>');
            });
            if (currentVal) selectElement.val(currentVal);
        },
        error: function (xhr, status, error) {
            console.error('Error fetching statuses: ' + error);
        }
    });
}

function refreshOrderStatusCounts() {
    if (!_orderStatusesCache || $('#OrderStatusId').length === 0) return;

    var params = {};
    var filterMap = {
        '#CountryId': 'countryId',
        '#CityId': 'cityId',
        '#ManufacturingCompanyId': 'storeId',
        '#DeliveryCompanyId': 'deliverycompanyId',
        '#DeliveryRepresentativeId': 'deliveryrepresentativeId',
        '#searchInput': 'search',
        '#OrderSourceId': 'ordersourceId',
        '#FailureReasonId': 'failureReason',
        '#EmployeeId': 'employeeId',
        '#ProductId': 'productId',
        '#GenderId': 'gender',
        '#startDate': 'startDate',
        '#endDate': 'endDate'
    };
    $.each(filterMap, function (selector, paramName) {
        var $el = $(selector);
        if ($el.length && $el.val()) params[paramName] = $el.val();
    });
    // Boolean filters (Home page toggles)
    if (typeof isOffers !== 'undefined' && isOffers != null) params.isOffers = isOffers;
    if (typeof isDiscount !== 'undefined' && isDiscount != null) params.isDiscount = isDiscount;
    if (typeof isBonus !== 'undefined' && isBonus != null) params.isBonus = isBonus;
    if (typeof isspecialClients !== 'undefined' && isspecialClients != null) params.isspecialClients = isspecialClients;
    if (typeof isFixedAndDelivered !== 'undefined' && isFixedAndDelivered != null) params.isFixedAndDelivered = isFixedAndDelivered;
    if (typeof isPaid !== 'undefined' && isPaid != null) params.isPaid = isPaid;
    if (typeof isHidden !== 'undefined' && isHidden != null) params.isHidden = isHidden;
    if (typeof IsComplaints !== 'undefined' && IsComplaints != null) params.IsComplaints = IsComplaints;
    if (typeof currentOrderTypes !== 'undefined' && currentOrderTypes === 'comments') params.fromcomments = true;

    console.log('[refreshOrderStatusCounts] params:', params);

    $.ajax({
        url: '/Order/GetOrderStatusCounts',
        type: 'GET',
        data: params,
        dataType: 'json',
        success: function (counts) {
            var countMap = {};
            $.each(counts, function (_, item) {
                countMap[item.statusId] = item.count;
            });

            var selectElement = $('#OrderStatusId');
            selectElement.find('option').each(function (index) {
                var $opt = $(this);
                var id = $opt.val();
                if (!id) return;
                var count = countMap[parseInt(id)] || 0;
                $opt.data('count', count);

                // Update the live dropdown item if open
                var $li = $('.select2-results__option[data-select2-id*="-' + id + '"]');
                if (!$li.length) {
                    $li = $('.select2-results__option').eq(index);
                }
                if ($li.length) {
                    var $span = $li.find('.status-count');
                    if ($span.length) {
                        $span.text('(' + count + ')');
                    } else {
                        $li.find('.fw-bold').append('<span class="status-count text-muted" style="font-size:0.85em; margin-right:4px;">(' + count + ')</span>');
                    }
                }
            });
        }
    });
}

// Builds the current filter params object (shared by all count refresh helpers)
function _buildFilterParams(excludeSelector) {
    var params = {};
    var filterMap = {
        '#CountryId': 'countryId',
        '#CityId': 'cityId',
        '#ManufacturingCompanyId': 'storeId',
        '#DeliveryCompanyId': 'deliverycompanyId',
        '#DeliveryRepresentativeId': 'deliveryrepresentativeId',
        '#searchInput': 'search',
        '#OrderStatusId': 'orderstatusId',
        '#OrderSourceId': 'ordersourceId',
        '#FailureReasonId': 'failureReason',
        '#EmployeeId': 'employeeId',
        '#ProductId': 'productId',
        '#GenderId': 'gender',
        '#startDate': 'startDate',
        '#endDate': 'endDate'
    };
    $.each(filterMap, function (selector, paramName) {
        if (selector === excludeSelector) return; // skip the dimension being counted
        var $el = $(selector);
        if ($el.length && $el.val()) params[paramName] = $el.val();
    });
    if (typeof isOffers !== 'undefined' && isOffers != null) params.isOffers = isOffers;
    if (typeof isDiscount !== 'undefined' && isDiscount != null) params.isDiscount = isDiscount;
    if (typeof isBonus !== 'undefined' && isBonus != null) params.isBonus = isBonus;
    if (typeof isspecialClients !== 'undefined' && isspecialClients != null) params.isspecialClients = isspecialClients;
    if (typeof isFixedAndDelivered !== 'undefined' && isFixedAndDelivered != null) params.isFixedAndDelivered = isFixedAndDelivered;
    if (typeof isPaid !== 'undefined' && isPaid != null) params.isPaid = isPaid;
    if (typeof isHidden !== 'undefined' && isHidden != null) params.isHidden = isHidden;
    if (typeof IsComplaints !== 'undefined' && IsComplaints != null) params.IsComplaints = IsComplaints;
    if (typeof currentOrderTypes !== 'undefined' && currentOrderTypes === 'comments') params.fromcomments = true;
    return params;
}

// Refreshes counts on a dropdown via GetFilterCounts.
// dimension: one of "orderStatus","country","city","store","deliveryCompany","deliveryRepresentative","orderSource"
// selectId: the jQuery selector of the <select> (e.g. '#CountryId')
// The option values must match the numeric ids returned by the endpoint.
function refreshDropdownCounts(dimension, selectId) {
    var $select = $(selectId);
    if ($select.length === 0) return;

    var selectorMap = {
        orderStatus: '#OrderStatusId',
        country: '#CountryId',
        city: '#CityId',
        store: '#ManufacturingCompanyId',
        deliveryCompany: '#DeliveryCompanyId',
        deliveryRepresentative: '#DeliveryRepresentativeId',
        orderSource: '#OrderSourceId'
    };

    var params = _buildFilterParams(selectorMap[dimension]);
    params.dimension = dimension;

    $.ajax({
        url: '/Order/GetFilterCounts',
        type: 'GET',
        data: params,
        dataType: 'json',
        success: function (counts) {
            var countMap = {};
            $.each(counts, function (_, item) { countMap[item.id] = item.count; });

            $select.find('option').each(function () {
                var $opt = $(this);
                var id = $opt.val();
                if (!id) return;
                var count = countMap[id] != null ? countMap[id] : 0;
                $opt.data('count', count);
            });

            // If the dropdown is currently open, update the visible list items too
            $select.find('option').each(function (index) {
                var $opt = $(this);
                var id = $opt.val();
                if (!id) return;
                var count = $opt.data('count') || 0;
                var $li = $('.select2-results__option').eq(index);
                if ($li.length) {
                    var $span = $li.find('.status-count');
                    if ($span.length) {
                        $span.text('(' + count + ')');
                    } else {
                        $li.find('.fw-bold').append('<span class="status-count text-muted" style="font-size:0.85em; margin-right:4px;">(' + count + ')</span>');
                    }
                }
            });
        }
    });
}

// Refreshes counts on #FailureReasonId via GetFailureReasonCounts.
function refreshFailureReasonCounts() {
    var $select = $('#FailureReasonId');
    if ($select.length === 0 || !$select.is(':visible')) return;

    var params = _buildFilterParams('#FailureReasonId');

    $.ajax({
        url: '/Order/GetFailureReasonCounts',
        type: 'GET',
        data: params,
        dataType: 'json',
        success: function (counts) {
            var countMap = {};
            $.each(counts, function (_, item) { countMap[item.id] = item.count; });

            $select.find('option').each(function () {
                var $opt = $(this);
                var val = $opt.val();
                if (!val) return;
                var count = countMap[val] != null ? countMap[val] : 0;
                $opt.data('count', count);
            });

            // Update visible list items if open
            $select.find('option').each(function (index) {
                var $opt = $(this);
                var val = $opt.val();
                if (!val) return;
                var count = $opt.data('count') || 0;
                var $li = $('.select2-results__option').eq(index);
                if ($li.length) {
                    var $span = $li.find('.status-count');
                    if ($span.length) {
                        $span.text('(' + count + ')');
                    } else {
                        $li.find('.fw-bold').append('<span class="status-count text-muted" style="font-size:0.85em; margin-right:4px;">(' + count + ')</span>');
                    }
                }
            });
        }
    });
}

// Call all count refreshes for all visible Order Index dropdowns.
// Call this after loadData() completes so counts reflect the new result set.
function refreshAllOrderIndexCounts() {
    if ($('#OrderStatusId').length) refreshDropdownCounts('orderStatus', '#OrderStatusId');
    if ($('#CountryId').length) refreshDropdownCounts('country', '#CountryId');
    if ($('#CityId').length) refreshDropdownCounts('city', '#CityId');
    if ($('#ManufacturingCompanyId').length) refreshDropdownCounts('store', '#ManufacturingCompanyId');
    if ($('#DeliveryCompanyId').length) refreshDropdownCounts('deliveryCompany', '#DeliveryCompanyId');
    if ($('#DeliveryRepresentativeId').length) refreshDropdownCounts('deliveryRepresentative', '#DeliveryRepresentativeId');
    if ($('#OrderSourceId').length) refreshDropdownCounts('orderSource', '#OrderSourceId');
    refreshFailureReasonCounts();
}

function populateWorkshift() {
    var workshiftElement = $('#WorkShift');
    workshiftElement.append('<option value="" data-image="/path/to/your/image1.jpg"></option>');

    workshiftElement.append('<option value="true" data-image="/path/to/your/image1.jpg">صبح</option>');
    workshiftElement.append('<option value="false" data-image="/path/to/your/image2.jpg">مساء</option>');
}

function populateGender() {
    var genderElement = $('#GenderId');
    genderElement.append('<option value="" data-image="/path/to/your/image1.jpg"><option>');

    genderElement.append('<option value="true" data-image="/path/to/your/image3.jpg">ذكر</option>');
    genderElement.append('<option value="false" data-image="/path/to/your/image4.jpg">أنثى</option>');
}

function populateFixedOrders() {
    var FixedElement = $('#isFixed');
    FixedElement.append('<option value="" data-image="/path/to/your/image1.jpg"><option>');

    FixedElement.append('<option value="true" data-image="/path/to/your/image3.jpg">نعم</option>');
    FixedElement.append('<option value="false" data-image="/path/to/your/image4.jpg">لا</option>');
}

function populatePotentialOrderStatuses() {
    var statusElement = $('#StatusId');
    if (statusElement.length === 0) return;
    statusElement.empty();
    statusElement.append('<option value="">  </option>');
    statusElement.append('<option value="0">عميل محتمل</option>');
    statusElement.append('<option value="1">الرجوع للزبون</option>');
    statusElement.append('<option value="2">تم إرسال العرض 1</option>');
    statusElement.append('<option value="3">تم إرسال العرض 2</option>');
    statusElement.append('<option value="4">تم إرسال العرض 3</option>');
    statusElement.append('<option value="5">تم إرسال العرض 4</option>');
    statusElement.append('<option value="6">تم إرسال العرض 5</option>');
    statusElement.append('<option value="7">تم إرسال العرض 6</option>');
}

function fetchFailureReasons(selectorId, callback) {
    var targetSelector = selectorId || '#statusChangeReason';
    $.ajax({
        url: '/DataList/GetAllFailureReasons',
        type: 'GET',
        dataType: 'json',
        success: function (data) {
            var selectElement = $(targetSelector);
            selectElement.empty();
            selectElement.append('<option value="">  </option>');
            $.each(data, function (index, reason) {
                selectElement.append('<option value="' + reason.name + '">' + reason.name + '</option>');
            });
            if (typeof callback === 'function') callback();
        },
        error: function (xhr, status, error) {
            console.error('Error fetching failure reasons: ' + error);
        }
    });
}

function populatePotentialOrderStores() {
    var storeElement = $('#StoreNameId');
    if (storeElement.length === 0) return;
    $.ajax({
        url: '/DataList/GetAllStores',
        type: 'GET',
        dataType: 'json',
        success: function (data) {
            storeElement.empty();
            storeElement.append('<option value=""></option>');
            $.each(data, function (index, store) {
                storeElement.append('<option value="' + store.id + '" data-image="/' + store.logoUrl + '">' + store.name + '</option>');
            });
        },
        error: function (xhr, status, error) {
            console.error('Error fetching stores: ' + error);
        }
    });
}
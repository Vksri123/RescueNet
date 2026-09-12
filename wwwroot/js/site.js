// RescueNet Site-wide SignalR Connection Manager

(function () {
    console.log("RescueNet Core JS initialized.");

    // Initialize global signalr connection if the script is loaded
    if (typeof signalR !== 'undefined') {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/emergencyHub")
            .withAutomaticReconnect()
            .build();

        window.rescueHub = connection;

        connection.start()
            .then(() => {
                console.log("SignalR connected to RescueNet EmergencyHub successfully.");
            })
            .catch(err => {
                console.error("SignalR connection failed: ", err.toString());
            });
    } else {
        console.warn("SignalR script is not loaded. Real-time updates will be offline.");
    }
})();

// Browser Geolocation Helper
function getBrowserLocation(callback) {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            (position) => {
                callback({
                    success: true,
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude
                });
            },
            (error) => {
                let msg = "Geolocation error: ";
                switch(error.code) {
                    case error.PERMISSION_DENIED:
                        msg += "User denied the request for Geolocation.";
                        break;
                    case error.POSITION_UNAVAILABLE:
                        msg += "Location information is unavailable.";
                        break;
                    case error.TIMEOUT:
                        msg += "The request to get user location timed out.";
                        break;
                    default:
                        msg += "An unknown error occurred.";
                        break;
                }
                callback({ success: false, message: msg });
            },
            { enableHighAccuracy: true, timeout: 5000, maximumAge: 0 }
        );
    } else {
        callback({ success: false, message: "Geolocation is not supported by this browser." });
    }
}

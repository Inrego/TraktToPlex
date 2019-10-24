"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/migration").build();

connection.on("UpdateProgress", logMessage);

connection.start().then(function () {
    
}).catch(function (err) {
    logMessage("Error: " + err.toString());
    return console.error(err.toString());
});

function startMigration() {
    var plexKey = document.getElementById('PlexServerKey').value;
    var traktKey = document.getElementById('TraktKey').value;
    var plexServer = document.getElementById('PlexServer').value;

    connection.invoke("StartMigration", traktKey, plexKey, plexServer).catch(function (err) {
        logMessage("Error: " + err.toString());
        return console.error(err.toString());
    });
}

function logMessage(message) {
    var status = document.getElementById("migrationStatus");
    status.value += '\n' + message;
    status.scrollTop = status.scrollHeight;
}
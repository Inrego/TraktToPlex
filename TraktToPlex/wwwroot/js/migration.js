"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/migration").build();

connection.on("UpdateProgress", function (progress) {
    var status = document.getElementById("migrationStatus");
    status.value += '\n' + progress;
    status.scrollTop = status.scrollHeight;
});

connection.start().then(function () {
    
}).catch(function (err) {
    return console.error(err.toString());
});

function startMigration() {
    var plexKey = document.getElementById('PlexKey').value;
    var traktKey = document.getElementById('TraktKey').value;
    var plexServer = document.getElementById('PlexServer').value;

    connection.invoke("StartMigration", traktKey, plexKey, plexServer).catch(function (err) {
        return console.error(err.toString());
    });
}
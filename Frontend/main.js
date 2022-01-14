/* global bootstrap: false */
(function () {
  'use strict'
  var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
  tooltipTriggerList.forEach(function (tooltipTriggerEl) {
    new bootstrap.Tooltip(tooltipTriggerEl)
  })
})()

$('textarea').keyup(function () {
  var characterCount = $(this).val().length,
    current = $('#current'),
    maximum = $('#maximum'),
    theCount = $('#the-count');
  current.text(characterCount);
  /*This isn't entirely necessary, just playin around*/
  if (characterCount < 70) {
    current.css('color', '#666');
  }
  if (characterCount > 70 && characterCount < 90) {
    current.css('color', '#6d5555');
  }
  if (characterCount > 90 && characterCount < 100) {
    current.css('color', '#793535');
  }
  if (characterCount > 100 && characterCount < 120) {
    current.css('color', '#841c1c');
  }
  if (characterCount > 120 && characterCount < 139) {
    current.css('color', '#8f0001');
  }
  if (characterCount >= 140) {
    maximum.css('color', '#8f0001');
    current.css('color', '#8f0001');
    theCount.css('font-weight', 'bold');
  } else {
    maximum.css('color', '#666');
    theCount.css('font-weight', 'normal');
  }
});

function redirectToLogin() {
  $.removeCookie('username');
  $.cookie('remember', false);
  location.replace('/login');
}

$(document).ready(function(){
  $(".menu-username").html($.cookie('username'));
})

$(".signout-button").click(function(e){
  e.preventDefault();
  var obj = new Object();
  obj.user = username;
  const response = JSON.stringify(obj);
  $.ajax({
    type: "POST",
    url: location.href + 'signout',
    data: response,
    dataType: 'json',
    contentType: 'application/json;charset=UTF-8'
  }).done(function (data) {
    if (data.message == "success") {
        console.log(data);
    }
  });
  redirectToLogin();
});

$(".tweet-btn").click(function(e){
  e.preventDefault();
  var tweet = $("textarea.tweet-content").val();
  var obj = new Object();
  obj.username = username;
  obj.tweet = tweet;
  const response = JSON.stringify(obj);
  $.ajax({
    type: "POST",
    url: location.href + 'tweet',
    data: response,
    dataType: 'json',
    contentType: 'application/json;charset=UTF-8'
  }).done(function (data) {
    if (data.message == "success") {
        console.log(data);
    }
  });
  $("#the-textarea").val('');
});

var wsUri = "ws://" + location.host + "/livefeed";
var feeder;
window.addEventListener("load", init, false);

function init() {
  feeder = $('.livefeed-results');
  WebSocketInit();
}

function addFeed(msg) {
  feeder.prepend("<p>" + msg + "</p>")
}

function WebSocketInit() {
  socket = new WebSocket(wsUri);

  socket.onopen = function (e) {
    console.log("WEBSOCKET CONNECTED");
    socket.send(JSON.stringify({
      "task": "",
      "username": username,
      "value": ""
    }));
  }

  socket.onclose = function (e) {
    console.log("WEBSOCKET DISCONNECTED");
    socket.close();
    redirectToLogin();
  }

  socket.onmessage = function (e) {
    var response = JSON.parse(e.data);
    console.log(response);
    addFeed(response);
  }

  socket.onerror = function (e) {
    console.log("ERROR: " + e.data);
  }
}

function dw(what) { document.write(what); }

function QueryString(key) 
{ 
var value = null; 
for (var i=0;i<QueryString.keys.length;i++) 
{ 
if (QueryString.keys[i]==key) 
{ 
value = QueryString.values[i]; 
break; 
} 
} 
return value; 
} 
QueryString.keys = new Array(); 
QueryString.values = new Array(); 

function QueryString_Parse() 
{ 
QueryString.keys = new Array(); 
QueryString.values = new Array(); 

var query = unescape(window.location.search.substring(1).replace( /\+/g , ' ')); 
var pairs = query.split("&"); 

for (var i=0;i<pairs.length;i++) 
{ 
var pos = pairs[i].indexOf('='); 
if (pos >= 0) 
{ 
var argname = pairs[i].substring(0,pos); 
var value = pairs[i].substring(pos+1); 
QueryString.keys[QueryString.keys.length] = argname; 
QueryString.values[QueryString.values.length] = value; 
} 
} 
}

var urlsplit=top.location.href.split("?");
var searchfor="";
var parname="name";
var qstyle='0';

function ValidatePars(sf,qs)
{
 if (sf) searchfor = sf;
 if (qs) {
    var lqs = qs.toString();
    if (lqs=='0' || lqs=='1' || lqs=='2') qstyle = lqs;
 }
}

function strTrim(s) {
  s = s.replace( /^\s+/g, "" );
  return s.replace( /\s+$/g, "" );
}

if (iframes==0)
 dw("<br><center><font size=+2>Sorry, you need an iframes-enabled browser to view this page properly.</font></center><br><br>");
else
{
 var pset,rv,pcs;
 QueryString_Parse(); 
 ValidatePars(QueryString(parname),QueryString('cs'));

 var checked=new Array('','','');
 checked[qstyle]=' checked';

 dw('<center>');

 dw('<form name=query action='+urlsplit[0]+' method=get>');
 dw('<input type="text" name="name" size="50" value="'+ searchfor.replace( /\"/g ,"&quot;") +'">');
 dw('&nbsp;&nbsp;<input type="submit" value="Kimi, have you rolled it?"><br>');
 dw(' <input type="radio" name="cs" value="0"'+checked[0]+'>Find item names that <b>contain</b> this string<br>');
 dw(' <input type="radio" name="cs" value="2"'+checked[2]+'>Exact item name<br>');
 dw(' <input type="radio" name="cs" value="1"'+checked[1]+'>ClickSaver (Google-like) item match string syntax, i.e. <b>plunder skills -weak -lesser</b><br>');
 dw(' All search options are case insensitive.<br>');
 dw(" Tip: Use ClikSaver (Google-like) mode if you don't get any result with the other modes.<br>");
 dw('</form>');

 searchfor = strTrim(searchfor);
 if (searchfor!="")
 {
  var url='https://javierarpa.com/cgi-bin/aorollq.cgi?cs='+qstyle+'&'+parname+'='+escape(searchfor);
  //url += '&ip='+escape(fromip);
  dw('<br><iframe src="'+url+'" width=640 height=400 scrolling=auto hspace=0 vspace=0 frameborder=1 marginheight=0 marginwidth=0></iframe>');

  dw("<br><br>If the query results above show I've rolled the item of your interest, the following values reveal its <b>roll-a-rarity</b>:<br><br>");
  dw("<b>Average (x5) rolls</b>: The number of rolls you'll probably need to roll the item as mission reward, assuming you're rolling solo missions, 5 rewards per roll.<br>");
  dw("<br><img src='/ao/img/cs-mish-settings1.jpg' width='101' height='173' align='left'>");
  dw("<b>Average (x4) rolls</b>: The number of rolls you'll probably need to roll the item as item-to-find, assuming you're getting 4 items-to-find per roll. You can get them with the following mission settings:<br>");
  dw("<br><b>Good/Bad</b> set to 100% (Bad)<br><b>Money/XP</b> set to 0% (Money)<br><br clear='left'>");
 }
 dw('</center>');
}

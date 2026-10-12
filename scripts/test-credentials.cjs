// Test-only: reuse current local seed credentials without logging them or modifying seeding.
const fs=require('node:fs'),path=require('node:path');
module.exports=function(){
 if(process.env.HIRING_TEST_EMAIL&&process.env.HIRING_TEST_PASSWORD)return {email:process.env.HIRING_TEST_EMAIL,password:process.env.HIRING_TEST_PASSWORD};
 const seedPath=path.join(__dirname,'../src/HiringPs.Web/SeedData.cs');
 if(fs.existsSync(seedPath)){const text=fs.readFileSync(seedPath,'utf8'),email=text.match(/(?:string|var)\s+AdminEmail\s*=\s*"([^"\r\n]+)"/),password=text.match(/var\s+password\s*=\s*("(?:\\.|[^"\\])*")/);if(email&&password)return {email:email[1],password:JSON.parse(password[1])};}
 const secret=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'Microsoft/UserSecrets/HiringPs-local-development/secrets.json'),'utf8').replace(/^\uFEFF/,''));return {email:secret['Bootstrap:AdminEmail'],password:secret['Bootstrap:AdminPassword']};
};

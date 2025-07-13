/* file: my-template/public/main.js */
//import bicep from './bicep.js'

export default {
  defaultTheme: 'dark',
  iconLinks: [
    {
      icon: 'github',
      href: 'https://github.com/taublast/DrawnUi',
      title: 'GitHub'
    }
  ],
   lunrLanguages: ['en', 'ru'],
  start() {
    console.log('started');
  },
  // configureHljs (hljs) {
  //   hljs.registerLanguage('bicep', bicep);
  // },
}

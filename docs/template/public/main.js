import WorkflowContainer from "./workflow.js"

export default {
    defaultTheme: 'light',
    iconLinks: [{
        icon: 'github',
        href: 'https://github.com/bruno-f-cruz/BFC.ObisLaser',
        title: 'GitHub'
    }],
    start: () => {
        WorkflowContainer.init();
    }
}
